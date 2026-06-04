import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import Popover from '@mui/material/Popover'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import { useCallback, useMemo, useRef, useState, type MouseEvent, type ReactNode } from 'react'
import { useCreateTag, useDeleteTag, useBoardTags, useUpdateTag } from '@/features/cards'
import { useUiStore, type TagDisplayMode } from '@/stores/uiStore'
import type { Guid, Tag } from '@/lib/types'

type TagManagementDialogProps = {
  open: boolean
  boardId: Guid
  canManageTags: boolean
  onClose: () => void
}

type TagDraft = {
  name: string
  color: string
}

const HEX_COLOR_PATTERN = /^#([0-9a-fA-F]{6})$/

const DEFAULT_TAG_COLOR = '#3b82f6'

const PRESET_COLORS = [
  '#ef4444',
  '#f97316',
  '#f59e0b',
  '#84cc16',
  '#22c55e',
  '#14b8a6',
  '#06b6d4',
  '#3b82f6',
  '#6366f1',
  '#8b5cf6',
  '#d946ef',
  '#ec4899',
  '#78716c',
  '#6b7280',
]

function isValidHexColor(color: string): boolean {
  return HEX_COLOR_PATTERN.test(color.trim())
}

type ColorPickerButtonProps = {
  value: string
  onChange: (color: string) => void
  disabled?: boolean
  size?: number
}

function ColorPickerButton({ value, onChange, disabled, size = 32 }: ColorPickerButtonProps) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const nativeInputRef = useRef<HTMLInputElement>(null)

  const handleOpen = (event: MouseEvent<HTMLElement>) => {
    if (!disabled) {
      setAnchorEl(event.currentTarget)
    }
  }

  const handleClose = () => {
    setAnchorEl(null)
  }

  const handlePresetClick = (color: string) => {
    onChange(color)
    handleClose()
  }

  const handleNativeChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      onChange(event.target.value)
    },
    [onChange],
  )

  const displayColor = isValidHexColor(value) ? value : '#9e9e9e'

  return (
    <>
      <Tooltip title="Pick color" arrow>
        <Box
          onClick={handleOpen}
          aria-label="Pick tag color"
          sx={{
            width: size,
            height: size,
            borderRadius: 1,
            bgcolor: displayColor,
            cursor: disabled ? 'default' : 'pointer',
            flexShrink: 0,
            '&:hover': disabled
              ? {}
              : {
                  boxShadow: '0 0 0 2px currentColor',
                },
          }}
        />
      </Tooltip>
      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
        transformOrigin={{ vertical: 'top', horizontal: 'left' }}
        slotProps={{ paper: { sx: { p: 1.5, overflow: 'hidden' } } }}
      >
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: 'repeat(7, auto)',
            gap: 0.75,
          }}
        >
          {PRESET_COLORS.map((color) => (
            <Tooltip key={color} title={color} arrow>
              <Box
                onClick={() => handlePresetClick(color)}
                aria-label={`Select color ${color}`}
                sx={{
                  width: 28,
                  height: 28,
                  borderRadius: 1,
                  bgcolor: color,
                  cursor: 'pointer',
                  border: 2,
                  borderColor: value === color ? 'text.primary' : 'transparent',
                  '&:hover': {
                    borderColor: 'text.primary',
                  },
                }}
              />
            </Tooltip>
          ))}
        </Box>
        <input
          ref={nativeInputRef}
          type="color"
          value={displayColor}
          onChange={handleNativeChange}
          style={{ position: 'absolute', opacity: 0, pointerEvents: 'none', width: 0, height: 0 }}
        />
      </Popover>
    </>
  )
}

export function TagManagementDialog({ open, boardId, canManageTags, onClose }: TagManagementDialogProps) {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  const tagsQuery = useBoardTags(boardId)
  const createTagMutation = useCreateTag()
  const updateTagMutation = useUpdateTag()
  const deleteTagMutation = useDeleteTag()

  const tagDisplayModes = useUiStore((state) => state.tagDisplayModes)
  const setTagDisplayMode = useUiStore((state) => state.setTagDisplayMode)

  const tags = useMemo(() => tagsQuery.data ?? [], [tagsQuery.data])

  const [createName, setCreateName] = useState('')
  const [createColor, setCreateColor] = useState(DEFAULT_TAG_COLOR)
  const [drafts, setDrafts] = useState<Record<string, TagDraft>>({})
  const [deleteTarget, setDeleteTarget] = useState<Tag | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const isBusy = createTagMutation.isPending || updateTagMutation.isPending || deleteTagMutation.isPending

  const sortedTags = useMemo(() => [...tags].sort((left, right) => left.name.localeCompare(right.name)), [tags])

  const canCreateTag =
    canManageTags && createName.trim().length > 0 && isValidHexColor(createColor) && !createTagMutation.isPending

  const handleDialogClose = () => {
    if (isBusy) {
      return
    }

    setSubmitError(null)
    setCreateName('')
    setCreateColor(DEFAULT_TAG_COLOR)
    onClose()
  }

  const handleCreateTag = async () => {
    if (!canCreateTag) {
      return
    }

    setSubmitError(null)

    try {
      await createTagMutation.mutateAsync({
        boardId,
        data: {
          name: createName.trim(),
          color: createColor.trim(),
        },
      })

      setCreateName('')
      setCreateColor(DEFAULT_TAG_COLOR)
    } catch {
      setSubmitError('Unable to create tag. Please try again.')
    }
  }

  const handleAutoSave = async (tag: Tag, nextName: string, nextColor: string) => {
    if (!canManageTags || updateTagMutation.isPending) {
      return
    }

    const trimmedName = nextName.trim()
    const trimmedColor = nextColor.trim()

    if (trimmedName.length === 0 || !isValidHexColor(trimmedColor)) {
      return
    }

    if (trimmedName === tag.name && trimmedColor === tag.color) {
      return
    }

    setSubmitError(null)

    try {
      await updateTagMutation.mutateAsync({
        id: tag.id,
        boardId,
        data: {
          name: trimmedName,
          color: trimmedColor,
        },
      })
    } catch {
      setSubmitError('Unable to update tag. Please try again.')
    }
  }

  const handleDeleteTag = async () => {
    if (!deleteTarget || !canManageTags || deleteTagMutation.isPending) {
      return
    }

    setSubmitError(null)

    try {
      await deleteTagMutation.mutateAsync({
        id: deleteTarget.id,
        boardId,
      })
      setDeleteTarget(null)
    } catch {
      setSubmitError('Unable to delete tag. Please try again.')
    }
  }

  return (
    <>
      <Dialog open={open} onClose={handleDialogClose} fullWidth maxWidth="sm" fullScreen={isMobile}>
        <DialogTitle>Manage Tags</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 0.5 }}>
            {tagsQuery.isLoading ? <Typography color="text.secondary">Loading tags...</Typography> : null}

            {tagsQuery.isError ? <Alert severity="error">Unable to load tags.</Alert> : null}

            {submitError ? <Alert severity="error">{submitError}</Alert> : null}

            {canManageTags ? (
              <Stack spacing={1.25}>
                <Typography variant="subtitle2">Create tag</Typography>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1.25}
                  alignItems={{ xs: 'stretch', sm: 'center' }}
                >
                  <Stack direction="row" spacing={1.25} alignItems="center" sx={{ flex: 1, minWidth: 0 }}>
                    <ColorPickerButton
                      value={createColor}
                      onChange={setCreateColor}
                      disabled={isBusy}
                      size={36}
                    />
                    <TextField
                      label="Name"
                      value={createName}
                      onChange={(event) => setCreateName(event.target.value)}
                      disabled={isBusy}
                      fullWidth
                    />
                  </Stack>
                  <Button
                    variant="contained"
                    onClick={handleCreateTag}
                    disabled={!canCreateTag}
                    sx={{ whiteSpace: 'nowrap', alignSelf: { xs: 'stretch', sm: 'auto' } }}
                  >
                    {createTagMutation.isPending ? 'Creating...' : 'Create'}
                  </Button>
                </Stack>
              </Stack>
            ) : null}

            <Stack spacing={1}>
              <Typography variant="subtitle2">Existing tags</Typography>

              {!tagsQuery.isLoading && sortedTags.length === 0 ? (
                <Typography color="text.secondary">No tags yet.</Typography>
              ) : null}

              {sortedTags.map((tag) => {
                const draft = drafts[tag.id] ?? { name: tag.name, color: tag.color }
                const currentMode = tagDisplayModes[tag.id] ?? 'both'

                return (
                  <CardLikeRow key={tag.id}>
                    {canManageTags ? (
                      <Stack spacing={1.25}>
                        <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                          <ColorPickerButton
                            value={draft.color}
                            onChange={(color) => {
                              setDrafts((previous) => ({
                                ...previous,
                                [tag.id]: {
                                  ...(previous[tag.id] ?? { name: tag.name, color: tag.color }),
                                  color,
                                },
                              }))
                              void handleAutoSave(tag, draft.name, color)
                            }}
                            disabled={isBusy}
                            size={32}
                          />
                          <TextField
                            label="Name"
                            size="small"
                            value={draft.name}
                            onChange={(event) =>
                              setDrafts((previous) => ({
                                ...previous,
                                [tag.id]: {
                                  ...(previous[tag.id] ?? { name: tag.name, color: tag.color }),
                                  name: event.target.value,
                                },
                              }))
                            }
                            onBlur={() => void handleAutoSave(tag, draft.name, draft.color)}
                            disabled={isBusy}
                            fullWidth
                          />
                        </Stack>
                        <Stack
                          direction="row"
                          spacing={1}
                          alignItems="center"
                          sx={{ flexWrap: 'wrap', gap: 1 }}
                        >
                          <Select
                            value={currentMode}
                            onChange={(event) => setTagDisplayMode(tag.id, event.target.value as TagDisplayMode)}
                            size="small"
                            aria-label={`Display mode for ${tag.name}`}
                            sx={{ flex: { xs: 1, sm: 'initial' }, minWidth: 140 }}
                          >
                            <MenuItem value="both">Name & color</MenuItem>
                            <MenuItem value="name">Name only</MenuItem>
                            <MenuItem value="color">Color only</MenuItem>
                          </Select>
                          <Button
                            variant="outlined"
                            size="small"
                            color="error"
                            onClick={() => setDeleteTarget(tag)}
                            disabled={isBusy}
                          >
                            Delete
                          </Button>
                        </Stack>
                      </Stack>
                    ) : (
                      <Stack
                        direction={{ xs: 'column', sm: 'row' }}
                        spacing={1.25}
                        alignItems={{ xs: 'stretch', sm: 'center' }}
                      >
                        <Stack direction="row" spacing={1.25} alignItems="center" sx={{ flex: 1, minWidth: 0 }}>
                          <Box
                            aria-label={`Tag color ${tag.name}`}
                            sx={{
                              width: 24,
                              height: 24,
                              borderRadius: 1,
                              bgcolor: tag.color,
                              flexShrink: 0,
                            }}
                          />
                          <Typography sx={{ flex: 1, minWidth: 0, wordBreak: 'break-word' }}>{tag.name}</Typography>
                        </Stack>
                        <Select
                          value={currentMode}
                          onChange={(event) => setTagDisplayMode(tag.id, event.target.value as TagDisplayMode)}
                          size="small"
                          aria-label={`Display mode for ${tag.name}`}
                          sx={{ minWidth: 140, alignSelf: { xs: 'stretch', sm: 'auto' } }}
                        >
                          <MenuItem value="both">Name & color</MenuItem>
                          <MenuItem value="name">Name only</MenuItem>
                          <MenuItem value="color">Color only</MenuItem>
                        </Select>
                      </Stack>
                    )}
                  </CardLikeRow>
                )
              })}
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleDialogClose} disabled={isBusy}>
            Close
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(deleteTarget) && canManageTags}
        onClose={() => {
          if (deleteTagMutation.isPending) {
            return
          }

          setDeleteTarget(null)
        }}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>Delete Tag</DialogTitle>
        <DialogContent>
          <DialogContentText>
            This will remove the tag from all cards.
            {deleteTarget ? ` Are you sure you want to delete "${deleteTarget.name}"?` : ''}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)} disabled={deleteTagMutation.isPending}>
            Cancel
          </Button>
          <Button onClick={handleDeleteTag} color="error" variant="contained" disabled={deleteTagMutation.isPending}>
            {deleteTagMutation.isPending ? 'Deleting...' : 'Delete'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}

type CardLikeRowProps = {
  children: ReactNode
}

function CardLikeRow({ children }: CardLikeRowProps) {
  return (
    <Box
      sx={{
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        p: 1.25,
      }}
    >
      {children}
    </Box>
  )
}
