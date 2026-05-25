import { useMemo, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import UpdateOutlinedIcon from '@mui/icons-material/UpdateOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import { alpha } from '@mui/material/styles'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import type { SelectChangeEvent } from '@mui/material/Select'
import type { ProjectActivity } from '@/lib/types'
import { formatRelativeDate } from '../utils/overviewUtils'

type EntityTypeFilter = 'card' | 'board' | 'workspace'

interface OverviewRecentActivityProps {
  items: ProjectActivity[]
  onItemClick?: (item: ProjectActivity) => void
}

const PAGE_SIZE = 5

const entityTypeConfig: Record<string, { label: string; color: string }> = {
  card: { label: 'Task', color: 'primary.main' },
  board: { label: 'Board', color: 'warning.main' },
  workspace: { label: 'Workspace', color: 'success.main' },
}

function formatActivityAction(action: string, field?: string | null, oldValue?: string | null, newValue?: string | null): string {
  if (action === 'created') return 'created this'
  if (action === 'archived') return 'archived this'
  if (action === 'restored') return 'restored this'

  if (action === 'moved' && field === 'list') {
    return `moved from ${oldValue ?? '?'} to ${newValue ?? '?'}`
  }

  if (action === 'added') {
    if (field === 'comment') return 'added a comment'
    if (field === 'subtask') return `added subtask "${newValue}"`
    if (field === 'tag') return `added tag "${newValue}"`
    if (field === 'assignee') return `assigned ${newValue}`
    if (field === 'attachment') return `attached "${newValue}"`
    if (field === 'google drive') return `linked "${newValue}"`
    return `added ${field ?? 'item'}`
  }

  if (action === 'removed') {
    if (field === 'comment') return 'removed a comment'
    if (field === 'subtask') return `removed subtask "${oldValue}"`
    if (field === 'tag') return `removed tag "${oldValue}"`
    if (field === 'assignee') return `unassigned ${oldValue}`
    if (field === 'attachment') return `removed attachment "${oldValue}"`
    if (field === 'google drive') return `unlinked "${oldValue}"`
    if (field === 'start date') return 'removed start date'
    if (field === 'due date') return 'removed due date'
    return `removed ${field ?? 'item'}`
  }

  if (action === 'changed') {
    if (field === 'title') return `renamed to "${newValue}"`
    if (field === 'description') return 'updated the description'
    if (field === 'start date') return `set start date to ${newValue}`
    if (field === 'due date') return `set due date to ${newValue}`
    return `changed ${field ?? 'field'}`
  }

  if (action === 'completed' && field === 'subtask') {
    return `completed subtask "${newValue}"`
  }

  if (action === 'uncompleted' && field === 'subtask') {
    return `reopened subtask "${newValue}"`
  }

  return action
}

const entityFilterOptions: { value: EntityTypeFilter; label: string }[] = [
  { value: 'card', label: 'Tasks' },
  { value: 'board', label: 'Boards' },
  { value: 'workspace', label: 'Workspace' },
]

export function OverviewRecentActivity({ items, onItemClick }: OverviewRecentActivityProps) {
  const [showCount, setShowCount] = useState(PAGE_SIZE)
  const [typeFilter, setTypeFilter] = useState<EntityTypeFilter | null>(null)

  const availableTypes = useMemo(
    () => new Set(items.map((i) => i.entityType)),
    [items],
  )

  const filtered = useMemo(
    () => (typeFilter ? items.filter((i) => i.entityType === typeFilter) : items),
    [items, typeFilter],
  )

  if (items.length === 0) return null

  const visible = filtered.slice(0, showCount)
  const remaining = filtered.length - showCount
  const hasMore = remaining > 0

  return (
    <Box>
      <Stack
        direction="row"
        spacing={1.5}
        useFlexGap
        alignItems="center"
        sx={{ mb: 2.5, flexWrap: 'wrap', rowGap: 1 }}
      >
        <UpdateOutlinedIcon sx={{ fontSize: 22, color: 'primary.main' }} />
        <Typography variant="h3" sx={{ fontWeight: 700 }}>
          Recent Activity
        </Typography>
        <Chip
          label={filtered.length}
          size="small"
          sx={{
            height: 24,
            fontSize: 12,
            fontWeight: 700,
            bgcolor: 'action.selected',
            color: 'text.primary',
          }}
        />
        {availableTypes.size >= 1 && (
          <Select
            size="small"
            value={typeFilter ?? '__all__'}
            onChange={(e: SelectChangeEvent) => {
              const v = e.target.value
              setTypeFilter(v === '__all__' ? null : v as EntityTypeFilter)
              setShowCount(PAGE_SIZE)
            }}
            sx={{
              ml: { xs: 0, sm: 'auto' },
              width: { xs: '100%', sm: 'auto' },
              minWidth: { xs: 0, sm: 100 },
              maxWidth: { xs: '100%', sm: 180 },
              height: 32,
              fontSize: 13,
              '& .MuiSelect-select': { overflow: 'hidden', textOverflow: 'ellipsis' },
            }}
          >
            <MenuItem value="__all__">All types</MenuItem>
            {entityFilterOptions
              .filter((opt) => availableTypes.has(opt.value))
              .map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>{opt.label}</MenuItem>
              ))}
          </Select>
        )}
      </Stack>

      <Stack spacing={1}>
        {visible.map((item) => {
          const config = entityTypeConfig[item.entityType] ?? entityTypeConfig.card
          return (
            <Box
              key={item.id}
              onClick={onItemClick ? () => onItemClick(item) : undefined}
              sx={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 2,
                px: 2,
                py: 1.5,
                borderRadius: 2,
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark'
                    ? 'rgba(255,255,255,0.03)'
                    : 'rgba(220,230,245,0.35)',
                border: '1px solid',
                borderColor: 'divider',
                ...(onItemClick && {
                  cursor: 'pointer',
                  '&:hover': { bgcolor: 'action.hover' },
                }),
              }}
            >
              <Box sx={{ minWidth: 0, flex: 1, overflow: 'hidden' }}>
                <Stack direction="row" spacing={0.75} alignItems="center" sx={{ minWidth: 0 }}>
                  <Chip
                    label={config.label}
                    size="small"
                    sx={{
                      height: 20,
                      fontSize: 11,
                      fontWeight: 700,
                      bgcolor: config.color,
                      color: 'common.white',
                      flexShrink: 0,
                      '& .MuiChip-label': { px: 0.75 },
                    }}
                  />
                  <Typography
                    variant="body2"
                    sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', minWidth: 0 }}
                  >
                    {item.entityName}
                  </Typography>
                </Stack>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                >
                  <Typography component="span" variant="caption" sx={{ fontWeight: 600, color: 'text.primary' }}>
                    {item.userName}
                  </Typography>
                  {' '}{formatActivityAction(item.action, item.field, item.oldValue, item.newValue)}
                </Typography>
                {item.entityType === 'card' && item.boardName && (
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', columnGap: 0.5, rowGap: 0.5, mt: 0.25 }}>
                    <Chip
                      icon={<ViewKanbanOutlinedIcon sx={{ fontSize: 12 }} />}
                      label={item.boardName}
                      size="small"
                      sx={{
                        height: 18,
                        fontSize: 10,
                        fontWeight: 600,
                        maxWidth: '100%',
                        bgcolor: (theme) => alpha(theme.palette.warning.main, 0.12),
                        color: 'warning.main',
                        '& .MuiChip-label': { px: 0.5 },
                        '& .MuiChip-icon': { color: 'warning.main', ml: 0.5, mr: -0.25 },
                      }}
                    />
                    <Chip
                      icon={<ViewColumnOutlinedIcon sx={{ fontSize: 12 }} />}
                      label={item.columnName}
                      size="small"
                      sx={{
                        height: 18,
                        fontSize: 10,
                        fontWeight: 600,
                        maxWidth: '100%',
                        bgcolor: (theme) => alpha(theme.palette.primary.main, 0.12),
                        color: 'primary.main',
                        '& .MuiChip-label': { px: 0.5 },
                        '& .MuiChip-icon': { color: 'primary.main', ml: 0.5, mr: -0.25 },
                      }}
                    />
                  </Box>
                )}
              </Box>
              <Chip
                label={formatRelativeDate(item.createdAt)}
                size="small"
                sx={{
                  height: 24,
                  fontSize: 12,
                  fontWeight: 600,
                  bgcolor: 'action.selected',
                  color: 'text.secondary',
                  flexShrink: 0,
                }}
              />
            </Box>
          )
        })}
      </Stack>

      {(hasMore || showCount > PAGE_SIZE) && (
        <Stack direction="row" spacing={1} sx={{ mt: 1.5 }}>
          {hasMore && (
            <Button
              size="small"
              onClick={() => setShowCount((v) => v + PAGE_SIZE)}
              endIcon={<ExpandMoreIcon />}
              sx={{ color: 'text.secondary' }}
            >
              {remaining <= PAGE_SIZE
                ? `Show ${remaining} more`
                : `Show ${PAGE_SIZE} more (${remaining - PAGE_SIZE} others)`}
            </Button>
          )}
          {showCount > PAGE_SIZE && (
            <Button
              size="small"
              onClick={() => setShowCount(PAGE_SIZE)}
              endIcon={<ExpandLessIcon />}
              sx={{ color: 'text.secondary' }}
            >
              Show less
            </Button>
          )}
        </Stack>
      )}
    </Box>
  )
}
