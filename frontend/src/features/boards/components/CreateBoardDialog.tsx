import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useCreateBoard, useCreateColumn } from '@/features/boards/api/boards'

const BOARD_TEMPLATES: { label: string; columns: string[] }[] = [
  { label: 'Empty board', columns: [] },
  { label: 'Kanban', columns: ['To Do', 'In Progress', 'Done'] },
  { label: 'Weekly Planner', columns: ['Today', 'This Week', 'Later', 'Done'] },
  { label: 'Sprint', columns: ['Backlog', 'To Do', 'In Progress', 'Review', 'Done'] },
]

interface CreateBoardDialogProps {
  open: boolean
  onClose: () => void
  projectId: string
}

export function CreateBoardDialog({ open, onClose, projectId }: CreateBoardDialogProps) {
  const createBoardMutation = useCreateBoard()
  const createColumnMutation = useCreateColumn()
  const [name, setName] = useState('')
  const [selectedTemplate, setSelectedTemplate] = useState(1)
  const isBusy = createBoardMutation.isPending || createColumnMutation.isPending

  const trimmed = name.trim()

  const handleCreate = async () => {
    if (!trimmed || isBusy) return

    const template = BOARD_TEMPLATES[selectedTemplate]
    const createdBoard = await createBoardMutation.mutateAsync({
      projectId,
      data: { name: trimmed },
    })

    if (template && template.columns.length > 0) {
      for (const columnName of template.columns) {
        try {
          await createColumnMutation.mutateAsync({
            boardId: createdBoard.id,
            data: { name: columnName },
          })
        } catch {
          // Continue even if one column fails
        }
      }
    }

    setName('')
    setSelectedTemplate(1)
    onClose()
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && trimmed) {
      void handleCreate()
    }
  }

  return (
    <Dialog
      open={open}
      onClose={() => {
        if (!isBusy) {
          setName('')
          setSelectedTemplate(1)
          onClose()
        }
      }}
      fullWidth
      maxWidth="xs"
      aria-labelledby="create-board-title"
    >
      <DialogTitle id="create-board-title">Create board</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ mt: 1 }}>
          <TextField
            label="Board name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={isBusy}
            inputRef={(input: HTMLInputElement | null) => {
              if (input && open) {
                requestAnimationFrame(() => input.focus())
              }
            }}
            fullWidth
          />

          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
              Start with a template
            </Typography>
            <Stack spacing={0.75}>
              {BOARD_TEMPLATES.map((template, index) => (
                <Box
                  key={template.label}
                  onClick={() => {
                    if (!isBusy) setSelectedTemplate(index)
                  }}
                  sx={{
                    p: 1.5,
                    borderRadius: 1,
                    border: '1px solid',
                    borderColor: selectedTemplate === index ? 'primary.main' : 'divider',
                    bgcolor:
                      selectedTemplate === index
                        ? (theme) =>
                            theme.palette.mode === 'dark'
                              ? 'rgba(20,184,166,0.08)'
                              : 'rgba(13,148,136,0.04)'
                        : 'transparent',
                    cursor: isBusy ? 'default' : 'pointer',
                    '&:hover': isBusy ? {} : { borderColor: 'primary.main' },
                  }}
                >
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {template.label}
                  </Typography>
                  {template.columns.length > 0 ? (
                    <Typography variant="caption" color="text.secondary">
                      {template.columns.join(' → ')}
                    </Typography>
                  ) : (
                    <Typography variant="caption" color="text.secondary">
                      No lists — start from scratch
                    </Typography>
                  )}
                </Box>
              ))}
            </Stack>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button
          onClick={() => {
            setName('')
            setSelectedTemplate(1)
            onClose()
          }}
          disabled={isBusy}
        >
          Cancel
        </Button>
        <Button onClick={handleCreate} variant="contained" disabled={!trimmed || isBusy}>
          {isBusy ? 'Creating...' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
