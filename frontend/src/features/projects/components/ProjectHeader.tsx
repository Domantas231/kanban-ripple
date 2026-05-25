import { useEffect, useRef, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CheckIcon from '@mui/icons-material/Check'
import CloseIcon from '@mui/icons-material/Close'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined'
import { useUpdateProject } from '@/features/projects/api/projects'
import { useUiStore } from '@/stores/uiStore'

interface ProjectHeaderProps {
  projectId: string
  name: string
  memberCount: number
  canEdit: boolean
}

export function ProjectHeader({ projectId, name, memberCount, canEdit }: ProjectHeaderProps) {
  const navigate = useNavigate()
  const updateProject = useUpdateProject()
  const enqueueToast = useUiStore((state) => state.enqueueToast)
  const [editing, setEditing] = useState(false)
  const [editValue, setEditValue] = useState(name)
  const [hovered, setHovered] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setEditValue(name)
  }, [name])

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus()
      inputRef.current?.select()
    }
  }, [editing])

  const handleSave = () => {
    const trimmed = editValue.trim()
    if (trimmed && trimmed !== name) {
      updateProject.mutate(
        { id: projectId, data: { name: trimmed } },
        {
          onSuccess: () =>
            enqueueToast({ message: 'Project renamed', severity: 'success', durationMs: 3000 }),
          onError: () => {
            setEditValue(name)
            enqueueToast({ message: 'Failed to rename project', severity: 'error' })
          },
        },
      )
    } else {
      setEditValue(name)
    }
    setEditing(false)
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSave()
    } else if (e.key === 'Escape') {
      setEditValue(name)
      setEditing(false)
    }
  }

  return (
    <Stack direction="row" alignItems="center" spacing={2} sx={{ minHeight: 48 }}>
      {editing ? (
        <Stack direction="row" alignItems="center" spacing={1} sx={{ flex: 1 }}>
          <TextField
            inputRef={inputRef}
            value={editValue}
            onChange={(e) => setEditValue(e.target.value)}
            onBlur={handleSave}
            onKeyDown={handleKeyDown}
            size="small"
            sx={{
              '& .MuiInputBase-input': {
                fontSize: '1.375rem',
                fontWeight: 600,
                lineHeight: 1.3,
                py: 0.5,
              },
              maxWidth: 400,
            }}
          />
          <IconButton size="small" onClick={handleSave} aria-label="Save name">
            <CheckIcon fontSize="small" />
          </IconButton>
          <IconButton
            size="small"
            onClick={() => {
              setEditValue(name)
              setEditing(false)
            }}
            aria-label="Cancel editing"
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Stack>
      ) : (
        <Stack
          direction="row"
          alignItems="center"
          spacing={1}
          onMouseEnter={() => setHovered(true)}
          onMouseLeave={() => setHovered(false)}
          sx={{ cursor: canEdit ? 'pointer' : 'default' }}
          onClick={() => {
            if (canEdit) setEditing(true)
          }}
        >
          <Typography variant="h3" sx={{ fontWeight: 700 }}>
            {name}
          </Typography>
          {canEdit && hovered ? (
            <IconButton size="small" aria-label="Edit project name" sx={{ ml: 0.5 }}>
              <EditOutlinedIcon sx={{ fontSize: 18 }} />
            </IconButton>
          ) : null}
        </Stack>
      )}

      <Chip
        icon={<GroupOutlinedIcon sx={{ fontSize: 18 }} />}
        label={`${memberCount} ${memberCount === 1 ? 'member' : 'members'}`}
        size="small"
        variant="outlined"
        onClick={() => navigate({ to: `/projects/${projectId}/settings` })}
        sx={{
          cursor: 'pointer',
          color: 'text.secondary',
          borderColor: 'divider',
          '&:hover': { bgcolor: 'action.hover' },
        }}
      />
    </Stack>
  )
}
