import { useState, type DragEvent } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/Close'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import { useImportTrelloBoard } from '@/features/boards/api/boards'

type TrelloPreview = {
  listCount: number
  archivedListCount: number
  cardCount: number
  archivedCardCount: number
  labelCount: number
  fileName: string
}

type TrelloListLike = { id?: string; name?: string; closed?: boolean; pos?: number }
type TrelloAttachmentLike = {
  id?: string
  name?: string
  url?: string
  mimeType?: string | null
  isUpload?: boolean
}
type TrelloCardLike = {
  id?: string
  name?: string
  desc?: string
  idList?: string
  closed?: boolean
  pos?: number
  idLabels?: string[]
  due?: string | null
  start?: string | null
  attachments?: TrelloAttachmentLike[]
}
type TrelloLabelLike = { id?: string; name?: string; color?: string | null }
type TrelloCheckItemLike = { id?: string; name?: string; state?: string; pos?: number }
type TrelloChecklistLike = {
  id?: string
  idCard?: string
  name?: string
  pos?: number
  checkItems?: TrelloCheckItemLike[]
}

type TrelloImportPayload = {
  name: string
  lists: TrelloListLike[]
  cards: TrelloCardLike[]
  labels: TrelloLabelLike[]
  checklists: TrelloChecklistLike[]
}

// Trello exports include a huge `actions` history plus per-card metadata the backend
// never reads. Strip the payload down to what BoardService.ImportFromTrelloAsync
// actually consumes so the upload and server-side deserialize stay small.
function slimTrelloPayload(data: Record<string, unknown>, name: string): TrelloImportPayload {
  const lists = (data.lists ?? []) as TrelloListLike[]
  const cards = (data.cards ?? []) as TrelloCardLike[]
  const labels = (data.labels ?? []) as TrelloLabelLike[]
  const checklists = (data.checklists ?? []) as TrelloChecklistLike[]

  return {
    name,
    lists: lists.map((l) => ({
      id: l.id,
      name: l.name,
      closed: l.closed,
      pos: l.pos,
    })),
    cards: cards.map((c) => ({
      id: c.id,
      name: c.name,
      desc: c.desc,
      idList: c.idList,
      closed: c.closed,
      pos: c.pos,
      idLabels: c.idLabels,
      due: c.due,
      start: c.start,
      attachments: (c.attachments ?? []).map((a) => ({
        id: a.id,
        name: a.name,
        url: a.url,
        mimeType: a.mimeType,
        isUpload: a.isUpload,
      })),
    })),
    labels: labels.map((l) => ({
      id: l.id,
      name: l.name,
      color: l.color,
    })),
    checklists: checklists.map((cl) => ({
      id: cl.id,
      idCard: cl.idCard,
      name: cl.name,
      pos: cl.pos,
      checkItems: (cl.checkItems ?? []).map((ci) => ({
        id: ci.id,
        name: ci.name,
        state: ci.state,
        pos: ci.pos,
      })),
    })),
  }
}

function buildPreview(data: Record<string, unknown>, fileName: string): TrelloPreview {
  const lists = (data.lists ?? []) as TrelloListLike[]
  const cards = (data.cards ?? []) as TrelloCardLike[]
  const labels = (data.labels ?? []) as unknown[]

  const archivedListIds = new Set(
    lists.filter((l) => l.closed).map((l) => l.id).filter((id): id is string => Boolean(id)),
  )
  // A card lands archived if it's archived itself OR its list is archived (cascade).
  const archivedCardCount = cards.filter(
    (c) => c.closed || (c.idList !== undefined && archivedListIds.has(c.idList)),
  ).length

  return {
    listCount: lists.length,
    archivedListCount: archivedListIds.size,
    cardCount: cards.length,
    archivedCardCount,
    labelCount: labels.length,
    fileName,
  }
}

interface TrelloImportDialogProps {
  open: boolean
  onClose: () => void
  projectId: string
}

export function TrelloImportDialog({ open, onClose, projectId }: TrelloImportDialogProps) {
  const importTrelloMutation = useImportTrelloBoard()

  const [preview, setPreview] = useState<TrelloPreview | null>(null)
  const [trelloData, setTrelloData] = useState<Record<string, unknown> | null>(null)
  const [boardName, setBoardName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [dragging, setDragging] = useState(false)

  const reset = () => {
    setPreview(null)
    setTrelloData(null)
    setBoardName('')
    setError(null)
    setDragging(false)
  }

  const handleClose = () => {
    reset()
    onClose()
  }

  const parseFile = async (f: File) => {
    setError(null)
    try {
      const text = await f.text()
      const data = JSON.parse(text) as Record<string, unknown>

      if (!data.lists || !data.cards) {
        setError('Invalid Trello export file. Expected "lists" and "cards" fields.')
        return
      }

      setTrelloData(data)
      setPreview(buildPreview(data, f.name))
      if (!boardName.trim()) {
        setBoardName(f.name.replace(/\.json$/i, ''))
      }
    } catch {
      setError('File is not valid JSON.')
    }
  }

  const handleFileInput = (event: React.ChangeEvent<HTMLInputElement>) => {
    const f = event.target.files?.[0]
    if (f) {
      void parseFile(f)
    }
    event.target.value = ''
  }

  const handleDragOver = (e: DragEvent) => {
    e.preventDefault()
    setDragging(true)
  }

  const handleDragLeave = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
  }

  const handleDrop = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
    const f = e.dataTransfer.files[0]
    if (f && f.name.endsWith('.json')) {
      void parseFile(f)
    } else {
      setError('Please drop a .json file.')
    }
  }

  const handleImport = async () => {
    if (!trelloData || !boardName.trim()) return

    const slimmed = slimTrelloPayload(trelloData, boardName.trim())

    await importTrelloMutation.mutateAsync({
      projectId,
      trelloData: slimmed,
    })

    handleClose()
  }

  const canImport =
    Boolean(trelloData) && boardName.trim().length > 0 && !importTrelloMutation.isPending

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm" aria-labelledby="trello-import-title">
      <DialogTitle id="trello-import-title">
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Import from Trello
        </Typography>
      </DialogTitle>
      <DialogContent>
        {!preview ? (
          <Box
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            sx={{
              border: 2,
              borderStyle: 'dashed',
              borderColor: dragging ? 'primary.main' : 'divider',
              borderRadius: 2,
              p: 5,
              textAlign: 'center',
              transition: 'border-color 150ms ease, background-color 150ms ease',
              bgcolor: dragging
                ? (theme) =>
                    theme.palette.mode === 'dark'
                      ? 'rgba(20,184,166,0.08)'
                      : 'rgba(13,148,136,0.04)'
                : 'transparent',
              cursor: 'pointer',
            }}
          >
            <UploadFileIcon sx={{ fontSize: 48, color: 'text.disabled', mb: 1.5 }} />
            <Typography variant="body1" sx={{ fontWeight: 500, mb: 0.5 }}>
              Drag & drop your Trello export
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              or click to browse for a .json file
            </Typography>
            <Button component="label" variant="outlined" size="small">
              Browse Files
              <input type="file" accept=".json" hidden onChange={handleFileInput} />
            </Button>
          </Box>
        ) : (
          <Stack spacing={2.5}>
            <Stack
              direction="row"
              alignItems="center"
              spacing={1.5}
              sx={{
                p: 2,
                border: 1,
                borderColor: 'divider',
                borderRadius: 1,
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
              }}
            >
              <UploadFileIcon sx={{ color: 'primary.main', fontSize: 24 }} />
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography variant="body2" sx={{ fontWeight: 500 }} noWrap>
                  {preview.fileName}
                </Typography>
              </Box>
              <IconButton size="small" onClick={reset} disabled={importTrelloMutation.isPending} aria-label="Remove file">
                <CloseIcon sx={{ fontSize: 16 }} />
              </IconButton>
            </Stack>

            <Box
              sx={{
                p: 2,
                borderRadius: 1,
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark' ? 'rgba(20,184,166,0.08)' : 'rgba(13,148,136,0.06)',
              }}
            >
              <Typography variant="body2" sx={{ fontWeight: 600, mb: 1 }}>
                Import preview
              </Typography>
              <Stack direction="row" spacing={3}>
                <PreviewStat
                  count={preview.listCount}
                  singular="List"
                  plural="Lists"
                  hint={preview.archivedListCount > 0 ? `${preview.archivedListCount} imported as archived` : undefined}
                />
                <PreviewStat
                  count={preview.cardCount}
                  singular="Card"
                  plural="Cards"
                  hint={preview.archivedCardCount > 0 ? `${preview.archivedCardCount} imported as archived` : undefined}
                />
                <PreviewStat count={preview.labelCount} singular="Label" plural="Labels" />
              </Stack>
            </Box>

            <TextField
              label="Board name"
              value={boardName}
              onChange={(e) => setBoardName(e.target.value)}
              disabled={importTrelloMutation.isPending}
              fullWidth
              size="small"
            />
          </Stack>
        )}

        {error ? (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        ) : null}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={handleClose} disabled={importTrelloMutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={handleImport}
          variant="contained"
          disabled={!canImport}
          startIcon={importTrelloMutation.isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {importTrelloMutation.isPending ? 'Importing...' : 'Import Board'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}

interface PreviewStatProps {
  count: number
  singular: string
  plural: string
  hint?: string
}

function PreviewStat({ count, singular, plural, hint }: PreviewStatProps) {
  return (
    <Stack alignItems="center">
      <Typography variant="h6" sx={{ fontWeight: 700 }}>
        {count}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {count === 1 ? singular : plural}
      </Typography>
      {hint ? (
        <Typography variant="caption" color="text.disabled" sx={{ fontSize: 11 }}>
          {hint}
        </Typography>
      ) : null}
    </Stack>
  )
}

