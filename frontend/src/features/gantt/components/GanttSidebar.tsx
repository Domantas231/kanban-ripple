import { useState } from 'react'
import AvatarGroup from '@mui/material/AvatarGroup'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Collapse from '@mui/material/Collapse'
import FormControl from '@mui/material/FormControl'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import type { SelectChangeEvent } from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import { BAR_HEIGHT } from '@/features/gantt/utils/ganttConstants'
import CloseIcon from '@mui/icons-material/Close'
import { alpha, useTheme } from '@mui/material/styles'
import { useDraggable } from '@dnd-kit/core'
import type { BoardSwimlane, Card } from '@/lib/types'

type GanttSidebarProps = {
  boards: BoardSwimlane[]
  collapsed?: boolean
  onToggleCollapse?: () => void
  onUnschedule?: (cardId: string) => void
  onCardClick?: (card: Card) => void
  selectedBoardId?: string
  onSelectBoard?: (boardId: string) => void
  canEdit?: boolean
}

type SidebarCardItemProps = {
  card: Card
  onUnschedule?: (cardId: string) => void
  onCardClick?: (card: Card) => void
  canEdit?: boolean
}

function SidebarCardItem({
  card,
  onUnschedule,
  onCardClick,
  canEdit = true,
}: SidebarCardItemProps) {
  const theme = useTheme()
  const isScheduled = Boolean(card.startDate && card.dueDate)
  const draggable = canEdit && !isScheduled

  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `sidebar-card-${card.id}`,
    data: { type: 'sidebar-card', card },
    disabled: !draggable,
  })

  return (
    <Box
      ref={draggable ? setNodeRef : undefined}
      {...(draggable ? { ...listeners, ...attributes } : {})}
      onClick={onCardClick ? () => onCardClick(card) : undefined}
      sx={{
        position: 'relative',
        p: 1,
        pl: 1.5,
        borderRadius: 1,
        bgcolor: isScheduled
          ? alpha(theme.palette.text.primary, 0.04)
          : alpha(theme.palette.primary.main, 0.08),
        border: 1,
        borderColor: isDragging
          ? 'primary.main'
          : isScheduled
            ? alpha(theme.palette.divider, 0.5)
            : alpha(theme.palette.primary.main, 0.4),
        cursor: draggable ? 'grab' : onCardClick ? 'pointer' : 'default',
        opacity: isDragging ? 0.4 : 1,
        transition: 'background-color 150ms, border-color 150ms',
        '&::before': {
          content: '""',
          position: 'absolute',
          left: 0,
          top: 0,
          bottom: 0,
          width: 3,
          borderTopLeftRadius: 4,
          borderBottomLeftRadius: 4,
          bgcolor: isScheduled ? 'transparent' : theme.palette.primary.main,
        },
        '&:hover': {
          borderColor: 'primary.main',
          bgcolor: isScheduled
            ? alpha(theme.palette.text.primary, 0.06)
            : alpha(theme.palette.primary.main, 0.12),
        },
      }}
    >
      <Stack direction="row" alignItems="flex-start" spacing={1}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography
            variant="body2"
            sx={{
              fontWeight: 500,
              fontSize: '0.8125rem',
              lineHeight: 1.4,
              wordBreak: 'break-word',
              color: isScheduled ? 'text.disabled' : 'text.primary',
              textDecoration: isScheduled ? 'line-through' : 'none',
              textDecorationColor: isScheduled
                ? alpha(theme.palette.text.disabled, 0.6)
                : undefined,
            }}
          >
            {card.title}
          </Typography>

          <Stack direction="row" alignItems="center" sx={{ mt: 0.5, flexWrap: 'wrap', gap: 0.5 }}>
            {card.cardTags?.map((ct) =>
              ct.tag ? (
                <Chip
                  key={ct.id}
                  size="small"
                  label={ct.tag.name}
                  sx={{
                    height: 20,
                    fontSize: '0.6875rem',
                    bgcolor: ct.tag.color
                      ? alpha(ct.tag.color, 0.15)
                      : alpha(theme.palette.text.primary, 0.08),
                    color: ct.tag.color || 'text.secondary',
                  }}
                />
              ) : null,
            )}
          </Stack>
        </Box>

        {card.assignments && card.assignments.length > 0 && (
          <AvatarGroup
            max={2}
            sx={{
              '& .MuiAvatar-root': {
                width: 22,
                height: 22,
                fontSize: '0.625rem',
                border: '1px solid',
                borderColor: 'common.white',
                boxSizing: 'border-box',
              },
            }}
          >
            {card.assignments.map((a) => (
              <Tooltip key={a.id} title={a.user?.userName ?? a.user?.email ?? ''}>
                <UserAvatar
                  userId={a.userId}
                  name={a.user?.userName ?? a.user?.email}
                  sx={{ width: 22, height: 22, fontSize: '0.625rem' }}
                />
              </Tooltip>
            ))}
          </AvatarGroup>
        )}

        {isScheduled && onUnschedule && (
          <Tooltip title="Remove from timeline">
            <IconButton
              size="small"
              aria-label={`Remove ${card.title} from timeline`}
              onClick={(e) => {
                e.stopPropagation()
                onUnschedule(card.id)
              }}
              sx={{
                p: 0.25,
                flexShrink: 0,
                opacity: 0,
                color: 'text.secondary',
                '.MuiBox-root:hover > .MuiStack-root > &': { opacity: 1 },
                '&:hover': { color: 'text.primary' },
              }}
            >
              <CloseIcon sx={{ fontSize: 16 }} />
            </IconButton>
          </Tooltip>
        )}
      </Stack>
    </Box>
  )
}

export function SidebarCardOverlay({ card, width = 180 }: { card: Card; width?: number }) {
  return (
    <Box
      sx={{
        px: 1,
        width,
        height: BAR_HEIGHT,
        borderRadius: '6px',
        bgcolor: 'primary.main',
        border: 1,
        borderColor: (t) => alpha(t.palette.primary.main, 0.8),
        cursor: 'grabbing',
        display: 'flex',
        alignItems: 'center',
        overflow: 'hidden',
        boxShadow: (t) => `0 4px 12px ${alpha(t.palette.common.black, 0.25)}`,
      }}
    >
      <Typography
        variant="caption"
        sx={{
          fontWeight: 600,
          color: (t) => t.palette.primary.contrastText,
          fontSize: '0.6875rem',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          flex: 1,
        }}
      >
        {card.title}
      </Typography>
    </Box>
  )
}

export function GanttSidebar({
  boards,
  collapsed,
  onToggleCollapse,
  onUnschedule,
  onCardClick,
  selectedBoardId,
  onSelectBoard,
  canEdit = true,
}: GanttSidebarProps) {
  const handleBoardChange = (event: SelectChangeEvent<string>) => {
    onSelectBoard?.(event.target.value)
  }
  const [expandedColumns, setExpandedColumns] = useState<Record<string, boolean>>({})

  const toggleColumn = (columnId: string) => {
    setExpandedColumns((prev) => ({ ...prev, [columnId]: !(prev[columnId] ?? false) }))
  }

  const width = collapsed ? 36 : 290

  return (
    <Box
      sx={{
        width,
        minWidth: width,
        borderRight: 1,
        borderColor: 'divider',
        bgcolor: (theme) => alpha(theme.palette.background.default, 0.6),
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        transition:
          'width 240ms cubic-bezier(0.16, 1, 0.3, 1), min-width 240ms cubic-bezier(0.16, 1, 0.3, 1)',
      }}
    >
      {collapsed && (
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            pt: 1,
          }}
        >
          <Tooltip title="Expand sidebar" placement="right">
            <IconButton
              size="small"
              onClick={onToggleCollapse}
              aria-label="expand sidebar"
              sx={{ p: 0.5 }}
            >
              <ChevronRightIcon sx={{ fontSize: 18 }} />
            </IconButton>
          </Tooltip>
        </Box>
      )}
      {!collapsed && (
        <Box
          sx={{
            width: 290,
            minWidth: 290,
            height: '100%',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            opacity: 0,
            animation: 'ganttSidebarFadeIn 240ms ease-out forwards',
            animationDelay: '80ms',
            '@keyframes ganttSidebarFadeIn': {
              from: { opacity: 0 },
              to: { opacity: 1 },
            },
          }}
        >
          {(onToggleCollapse || (onSelectBoard && boards.length > 0)) && (
            <Box
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 1,
                px: 1,
                py: 0.75,
                borderBottom: 1,
                borderColor: 'divider',
                flexShrink: 0,
              }}
            >
              {onSelectBoard && boards.length > 0 ? (
                <FormControl size="small" sx={{ flex: 1, minWidth: 0 }}>
                  <Select
                    id="gantt-sidebar-board-select"
                    value={
                      selectedBoardId && boards.some((b) => b.board.id === selectedBoardId)
                        ? selectedBoardId
                        : ''
                    }
                    onChange={handleBoardChange}
                    sx={{ fontSize: '0.8125rem' }}
                  >
                    {boards.map((b) => (
                      <MenuItem key={b.board.id} value={b.board.id} sx={{ fontSize: '0.8125rem' }}>
                        {b.board.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              ) : (
                <Box sx={{ flex: 1 }} />
              )}
              {onToggleCollapse && (
                <Tooltip title="Collapse sidebar" placement="left">
                  <IconButton
                    size="small"
                    onClick={onToggleCollapse}
                    aria-label="collapse sidebar"
                    sx={{ p: 0.25, flexShrink: 0 }}
                  >
                    <ChevronLeftIcon sx={{ fontSize: 18 }} />
                  </IconButton>
                </Tooltip>
              )}
            </Box>
          )}

          <Box
            sx={{
              pb: 0.5,
              flex: 1,
              overflowY: 'auto',
              minHeight: 0,
            }}
          >
            {boards
              .filter((b) => !selectedBoardId || b.board.id === selectedBoardId)
              .map((boardSwimlane) => {
                return (
                  <Box
                    key={boardSwimlane.board.id}
                    sx={{ borderBottom: 1, borderColor: 'divider' }}
                  >
                    <Box>
                      {boardSwimlane.columns.map((colSwimlane) => {
                        const isColExpanded = expandedColumns[colSwimlane.column.id] ?? false
                        return (
                          <Box key={colSwimlane.column.id}>
                            <Box
                              onClick={() => toggleColumn(colSwimlane.column.id)}
                              sx={{
                                display: 'flex',
                                alignItems: 'center',
                                pl: 1.5,
                                pr: 1,
                                py: 0.75,
                                cursor: 'pointer',
                                bgcolor: (theme) => alpha(theme.palette.text.primary, 0.06),
                                borderTop: 1,
                                borderBottom: 1,
                                borderColor: 'divider',
                                textTransform: 'uppercase',
                                letterSpacing: '0.04em',
                                '&:hover': {
                                  bgcolor: (theme) => alpha(theme.palette.text.primary, 0.1),
                                },
                              }}
                            >
                              {isColExpanded ? (
                                <ExpandMoreIcon
                                  sx={{ fontSize: 16, mr: 0.5, color: 'text.primary' }}
                                />
                              ) : (
                                <ChevronRightIcon
                                  sx={{ fontSize: 16, mr: 0.5, color: 'text.primary' }}
                                />
                              )}
                              <Typography
                                variant="body2"
                                sx={{
                                  fontWeight: 700,
                                  fontSize: '0.75rem',
                                  flex: 1,
                                  color: 'text.primary',
                                  letterSpacing: 'inherit',
                                  textTransform: 'inherit',
                                }}
                              >
                                {colSwimlane.column.name}
                              </Typography>
                              <Chip
                                size="small"
                                label={colSwimlane.cardCount}
                                sx={{
                                  height: 18,
                                  minWidth: 22,
                                  fontSize: '0.625rem',
                                  fontWeight: 600,
                                }}
                              />
                            </Box>

                            <Collapse in={isColExpanded}>
                              <Stack spacing={1} sx={{ p: 1 }}>
                                {colSwimlane.cards.map((card) => (
                                  <SidebarCardItem
                                    key={card.id}
                                    card={card}
                                    onUnschedule={onUnschedule}
                                    onCardClick={onCardClick}
                                    canEdit={canEdit}
                                  />
                                ))}
                              </Stack>
                            </Collapse>
                          </Box>
                        )
                      })}
                    </Box>
                  </Box>
                )
              })}
          </Box>
        </Box>
      )}
    </Box>
  )
}
