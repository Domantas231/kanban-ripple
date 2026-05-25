import AddIcon from '@mui/icons-material/Add'
import AttachFileOutlinedIcon from '@mui/icons-material/AttachFileOutlined'
import CalendarTodayOutlinedIcon from '@mui/icons-material/CalendarTodayOutlined'
import ChatBubbleOutlineOutlinedIcon from '@mui/icons-material/ChatBubbleOutlineOutlined'
import ChecklistRtlOutlinedIcon from '@mui/icons-material/ChecklistRtlOutlined'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined'
import SubjectOutlinedIcon from '@mui/icons-material/SubjectOutlined'
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import AvatarGroup from '@mui/material/AvatarGroup'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import InputBase from '@mui/material/InputBase'
import Typography from '@mui/material/Typography'
import { alpha } from '@mui/material/styles'
import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useUpdateCard } from '@/features/cards/api/cards'
import { TagChip } from '@/features/tags'
import { useUiStore } from '@/stores/uiStore'
import type { Card as KanbanCard, Guid } from '@/lib/types'

const TAG_DISPLAY_ORDER = { color: 0, both: 1, name: 2 } as const

const MAX_VISIBLE_TAGS = 3

const VIRTUALIZATION_THRESHOLD = 30
const CARD_ITEM_HEIGHT = 140
const CARD_LIST_MAX_HEIGHT = 520
const VIRTUAL_OVERSCAN = 4

type CardListProps = {
  columnId: Guid
  cards: KanbanCard[]
  searchQuery?: string
  filterTagIds?: Guid[]
  filterUserIds?: Guid[]
  filterColumnIds?: Guid[]
  draggingCardId?: Guid | null
  canArchiveCards?: boolean
  selectedCardIds?: Set<Guid>
  onToggleSelectCard?: (cardId: Guid) => void
  onArchiveCard?: (card: KanbanCard) => void
  onCardClick: (card: KanbanCard) => void
  onAddCard?: () => void
}

type CardListItemProps = {
  columnId: Guid
  card: KanbanCard
  draggingCardId?: Guid | null
  canArchive?: boolean
  isSelected?: boolean
  showCheckbox?: boolean
  onToggleSelect?: (cardId: Guid) => void
  onArchive?: (card: KanbanCard) => void
  onClick: (card: KanbanCard) => void
}

function getDueDateStatus(
  dueDate: string | null | undefined,
): 'overdue' | 'today' | 'upcoming' | null {
  if (!dueDate) return null
  const now = new Date()
  const due = new Date(dueDate)
  if (Number.isNaN(due.getTime())) return null

  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const todayEnd = new Date(todayStart)
  todayEnd.setDate(todayEnd.getDate() + 1)
  const upcomingEnd = new Date(todayStart)
  upcomingEnd.setDate(upcomingEnd.getDate() + 3)

  if (due < todayStart) return 'overdue'
  if (due < todayEnd) return 'today'
  if (due < upcomingEnd) return 'upcoming'
  return null
}

function formatDueDateShort(dueDate: string): string {
  const date = new Date(dueDate)
  if (Number.isNaN(date.getTime())) return dueDate
  const month = date.toLocaleString('en', { month: 'short' })
  return `${month} ${date.getDate()}`
}

function CardListItem({
  columnId,
  card,
  isSelected,
  showCheckbox,
  onToggleSelect,
  onClick,
}: CardListItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: `card-${card.id}`,
    data: {
      type: 'card',
      cardId: card.id,
      columnId,
    },
  })

  const tagDisplayModes = useUiStore((state) => state.tagDisplayModes)

  const [isEditingTitle, setIsEditingTitle] = useState(false)
  const [titleDraft, setTitleDraft] = useState(card.title)
  const [prevTitle, setPrevTitle] = useState(card.title)
  const titleInputRef = useRef<HTMLInputElement>(null)
  const titleClickTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const updateCard = useUpdateCard()

  if (card.title !== prevTitle) {
    setPrevTitle(card.title)
    setTitleDraft(card.title)
  }

  useEffect(() => {
    return () => {
      if (titleClickTimerRef.current) {
        clearTimeout(titleClickTimerRef.current)
      }
    }
  }, [])

  useEffect(() => {
    if (isEditingTitle && titleInputRef.current) {
      titleInputRef.current.focus()
      titleInputRef.current.select()
    }
  }, [isEditingTitle])

  const saveTitle = useCallback(() => {
    const trimmed = titleDraft.trim()
    if (trimmed && trimmed !== card.title) {
      updateCard.mutate({
        id: card.id,
        data: {
          title: trimmed,
          description: card.description,
          startDate: card.startDate,
          dueDate: card.dueDate,
          version: card.version,
        },
      })
    } else {
      setTitleDraft(card.title)
    }
    setIsEditingTitle(false)
  }, [titleDraft, card, updateCard])

  const handleTitleClick = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation()
      if (isDragging) return

      if (titleClickTimerRef.current) {
        clearTimeout(titleClickTimerRef.current)
        titleClickTimerRef.current = null
        onClick(card)
        return
      }

      titleClickTimerRef.current = setTimeout(() => {
        titleClickTimerRef.current = null
        setIsEditingTitle(true)
      }, 250)
    },
    [isDragging, card, onClick],
  )

  const handleTitleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault()
        saveTitle()
      } else if (e.key === 'Escape') {
        setTitleDraft(card.title)
        setIsEditingTitle(false)
      }
    },
    [saveTitle, card.title],
  )
  const assignments = card.assignments ?? []
  const attachments = card.attachments ?? []
  const comments = card.comments ?? []
  const subtasks = card.subtasks ?? []
  const driveLinks = card.googleDriveLinks ?? []
  const completedSubtasks = subtasks.filter((subtask) => subtask.completed).length

  const estimatedMinutes = (card.estimatedHours ?? 0) * 60
  const spentMinutes = card.spentMinutes ?? 0
  const scheduledMinutes = Math.max(card.scheduledMinutes ?? 0, spentMinutes)
  const hasEstimate = estimatedMinutes > 0
  const spentRatio = hasEstimate ? Math.min(spentMinutes / estimatedMinutes, 1) : 0
  const scheduledRatio = hasEstimate ? Math.min(scheduledMinutes / estimatedMinutes, 1) : 0
  const isEstimateComplete = hasEstimate && spentMinutes >= estimatedMinutes

  const sortedTags = useMemo(() => {
    return [...(card.cardTags ?? [])].sort((a, b) => {
      const modeA = (a.tag?.id ? tagDisplayModes[a.tag.id] : undefined) ?? 'both'
      const modeB = (b.tag?.id ? tagDisplayModes[b.tag.id] : undefined) ?? 'both'
      return TAG_DISPLAY_ORDER[modeA] - TAG_DISPLAY_ORDER[modeB]
    })
  }, [card.cardTags, tagDisplayModes])

  const visibleTags = sortedTags.slice(0, MAX_VISIBLE_TAGS)
  const hiddenTagCount = sortedTags.length - visibleTags.length

  const dueDateStatus = getDueDateStatus(card.dueDate)

  const hasMetadata =
    subtasks.length > 0 ||
    attachments.length > 0 ||
    comments.length > 0 ||
    driveLinks.length > 0 ||
    card.dueDate ||
    card.description ||
    assignments.length > 0

  return (
    <Card
      ref={setNodeRef}
      elevation={0}
      sx={{
        width: '100%',
        borderRadius: 1,
        transform: CSS.Transform.toString(transform),
        transition,
        overflow: 'hidden',
        bgcolor: 'background.paper',
        border: '1px solid',
        borderColor: isSelected ? 'primary.main' : 'divider',
        boxShadow: isDragging ? 0 : 1,
        opacity: isDragging ? 0.4 : 1,
        cursor: isDragging ? 'grabbing' : 'pointer',
        userSelect: 'none',
        WebkitUserSelect: 'none',
        WebkitTouchCallout: 'none',
        WebkitTapHighlightColor: 'transparent',
        '&:hover': isDragging
          ? {}
          : {
              boxShadow: 2,
              borderColor: 'primary.main',
            },
      }}
    >
      <Box
        {...attributes}
        {...listeners}
        onClick={() => {
          if (isDragging || isEditingTitle) {
            return
          }
          onClick(card)
        }}
        aria-label={`Open task ${card.title}`}
      >
        <CardContent sx={{ p: 1, '&:last-child': { pb: 1 }, minWidth: 0 }}>
          <Stack direction="row" spacing={0.75} alignItems="flex-start">
            {showCheckbox ? (
              <Checkbox
                size="small"
                checked={isSelected ?? false}
                onClick={(event) => {
                  event.stopPropagation()
                }}
                onChange={(event) => {
                  event.stopPropagation()
                  onToggleSelect?.(card.id)
                }}
                sx={{
                  p: 0,
                  mt: 0.125,
                  flexShrink: 0,
                }}
                aria-label={`Select task ${card.title}`}
              />
            ) : null}
            <Stack spacing={0.75} sx={{ minWidth: 0, flex: 1 }}>
              {/* Tags row */}
              {sortedTags.length > 0 ? (
                <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap" alignItems="center">
                  {visibleTags.map((cardTag) => {
                    const tag = cardTag.tag
                    if (!tag) return null
                    return (
                      <TagChip
                        key={cardTag.id}
                        tagId={tag.id}
                        tag={{ name: tag.name, color: tag.color }}
                        size="small"
                      />
                    )
                  })}
                  {hiddenTagCount > 0 ? (
                    <Chip
                      size="small"
                      label={`+${hiddenTagCount}`}
                      variant="outlined"
                      sx={{ height: 20, fontSize: '0.6875rem', fontWeight: 600 }}
                    />
                  ) : null}
                </Stack>
              ) : null}

              {/* Title: single click to edit, double click to open detail */}
              {isEditingTitle ? (
                <InputBase
                  inputRef={titleInputRef}
                  value={titleDraft}
                  onChange={(e) => setTitleDraft(e.target.value)}
                  onBlur={saveTitle}
                  onKeyDown={handleTitleKeyDown}
                  multiline
                  maxRows={2}
                  sx={{
                    fontWeight: 500,
                    wordBreak: 'break-word',
                    lineHeight: 1.4,
                    fontSize: '0.875rem',
                    p: 0,
                    alignSelf: 'flex-start',
                    maxWidth: '100%',
                    '& .MuiInputBase-input': {
                      p: 0,
                      minWidth: '2ch',
                      ['fieldSizing' as 'width']: 'content',
                    },
                  }}
                />
              ) : (
                <Typography
                  variant="body2"
                  onClick={handleTitleClick}
                  sx={{
                    fontWeight: 500,
                    wordBreak: 'break-word',
                    lineHeight: 1.4,
                    fontSize: '0.875rem',
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    overflow: 'hidden',
                    cursor: 'text',
                    borderRadius: 0.5,
                    alignSelf: 'flex-start',
                    width: 'fit-content',
                    maxWidth: '100%',
                    px: 0.5,
                    mx: -0.5,
                    '&:hover': {
                      bgcolor: (theme) => alpha(theme.palette.primary.main, 0.06),
                    },
                  }}
                >
                  {card.title}
                </Typography>
              )}

              {/* Bottom row: metadata badges left, avatars right */}
              {hasMetadata ? (
                <Stack
                  direction="row"
                  alignItems="center"
                  justifyContent="space-between"
                  spacing={0.5}
                  sx={{ minWidth: 0, width: '100%' }}
                >
                  <Stack
                    direction="row"
                    spacing={1}
                    alignItems="center"
                    sx={{ flexWrap: 'wrap', minWidth: 0, flex: 1 }}
                  >
                    {card.description ? (
                      <SubjectOutlinedIcon sx={{ fontSize: 15, color: 'text.disabled' }} />
                    ) : null}

                    {subtasks.length > 0 ? (
                      <Stack direction="row" spacing={0.375} alignItems="center">
                        <ChecklistRtlOutlinedIcon sx={{ fontSize: 14, color: 'text.disabled' }} />
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{ fontSize: '0.6875rem', fontWeight: 500, lineHeight: 1 }}
                        >
                          {completedSubtasks}/{subtasks.length}
                        </Typography>
                      </Stack>
                    ) : null}

                    {comments.length > 0 ? (
                      <Stack
                        direction="row"
                        spacing={0.375}
                        alignItems="center"
                        aria-label={`Comments: ${comments.length}`}
                      >
                        <ChatBubbleOutlineOutlinedIcon
                          sx={{ fontSize: 14, color: 'text.disabled' }}
                        />
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{ fontSize: '0.6875rem', fontWeight: 500, lineHeight: 1 }}
                        >
                          {comments.length}
                        </Typography>
                      </Stack>
                    ) : null}

                    {attachments.length > 0 ? (
                      <Stack
                        direction="row"
                        spacing={0.375}
                        alignItems="center"
                        aria-label={`Attachments: ${attachments.length}`}
                      >
                        <AttachFileOutlinedIcon sx={{ fontSize: 14, color: 'text.disabled' }} />
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{ fontSize: '0.6875rem', fontWeight: 500, lineHeight: 1 }}
                        >
                          {attachments.length}
                        </Typography>
                      </Stack>
                    ) : null}

                    {driveLinks.length > 0 ? (
                      <Stack
                        direction="row"
                        spacing={0.375}
                        alignItems="center"
                        aria-label={`Google Drive files: ${driveLinks.length}`}
                      >
                        <InsertDriveFileOutlinedIcon
                          sx={{ fontSize: 14, color: 'text.disabled' }}
                        />
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{ fontSize: '0.6875rem', fontWeight: 500, lineHeight: 1 }}
                        >
                          {driveLinks.length}
                        </Typography>
                      </Stack>
                    ) : null}
                  </Stack>

                  {assignments.length > 0 ? (
                    <AvatarGroup
                      max={2}
                      sx={{
                        flexShrink: 0,
                        '& .MuiAvatar-root': {
                          width: 24,
                          height: 24,
                          fontSize: 11,
                          fontWeight: 600,
                          bgcolor: 'primary.main',
                          color: 'primary.contrastText',
                          border: '1px solid',
                          borderColor: 'common.white',
                          boxSizing: 'border-box',
                        },
                      }}
                    >
                      {assignments.map((assignment) => (
                        <UserAvatar
                          key={assignment.id}
                          userId={assignment.userId}
                          name={assignment.user?.userName ?? assignment.user?.email}
                        />
                      ))}
                    </AvatarGroup>
                  ) : null}
                </Stack>
              ) : null}

              {/* Due date on its own line */}
              {card.dueDate ? (
                <Chip
                  size="small"
                  icon={<CalendarTodayOutlinedIcon sx={{ fontSize: '12px !important' }} />}
                  label={formatDueDateShort(card.dueDate)}
                  sx={{
                    height: 20,
                    fontSize: '0.6875rem',
                    fontWeight: 600,
                    alignSelf: 'flex-start',
                    '& .MuiChip-icon': { ml: 0.5 },
                    ...(dueDateStatus === 'overdue' && {
                      bgcolor: (theme) => alpha(theme.palette.error.main, 0.12),
                      color: 'error.main',
                      '& .MuiChip-icon': { color: 'error.main', ml: 0.5 },
                    }),
                    ...(dueDateStatus === 'today' && {
                      bgcolor: (theme) => alpha(theme.palette.warning.main, 0.12),
                      color: 'warning.dark',
                      '& .MuiChip-icon': { color: 'warning.dark', ml: 0.5 },
                    }),
                  }}
                />
              ) : null}
            </Stack>
          </Stack>
        </CardContent>
        {hasEstimate ? (
          <Box
            sx={{
              position: 'relative',
              height: 3,
              bgcolor: (theme) => alpha(theme.palette.primary.main, 0.08),
              overflow: 'hidden',
            }}
          >
            <Box
              aria-hidden
              sx={{
                position: 'absolute',
                left: 0,
                top: 0,
                bottom: 0,
                width: `${scheduledRatio * 100}%`,
                bgcolor: (theme) =>
                  alpha(theme.palette.primary.main, theme.palette.mode === 'dark' ? 0.28 : 0.22),
                transition: 'width 400ms cubic-bezier(0.4, 0, 0.2, 1)',
              }}
            />
            <Box
              aria-hidden
              sx={{
                position: 'absolute',
                left: 0,
                top: 0,
                bottom: 0,
                width: `${spentRatio * 100}%`,
                bgcolor: isEstimateComplete ? 'success.main' : 'primary.main',
                transition: 'width 400ms cubic-bezier(0.4, 0, 0.2, 1), background-color 200ms ease',
              }}
            />
          </Box>
        ) : null}
      </Box>
    </Card>
  )
}

const MemoizedCardListItem = memo(CardListItem)

function applyCardFilters(
  cards: KanbanCard[],
  searchQuery?: string,
  filterTagIds?: Guid[],
  filterUserIds?: Guid[],
  filterColumnIds?: Guid[],
) {
  const normalizedQuery = searchQuery?.trim().toLowerCase() ?? ''
  const hasQuery = normalizedQuery.length > 0
  const hasTagFilter = Boolean(filterTagIds && filterTagIds.length > 0)
  const hasUserFilter = Boolean(filterUserIds && filterUserIds.length > 0)
  const hasColumnFilter = Boolean(filterColumnIds && filterColumnIds.length > 0)

  return cards.filter((card) => {
    if (hasQuery) {
      const title = card.title.toLowerCase()
      const description = card.description?.toLowerCase() ?? ''
      if (!title.includes(normalizedQuery) && !description.includes(normalizedQuery)) {
        return false
      }
    }

    if (hasTagFilter) {
      const tagIds = new Set((card.cardTags ?? []).map((cardTag) => cardTag.tagId))
      if (!filterTagIds?.some((tagId) => tagIds.has(tagId))) {
        return false
      }
    }

    if (hasUserFilter) {
      const assigneeIds = new Set((card.assignments ?? []).map((assignment) => assignment.userId))
      if (!filterUserIds?.some((userId) => assigneeIds.has(userId))) {
        return false
      }
    }

    if (hasColumnFilter && !filterColumnIds?.includes(card.columnId)) {
      return false
    }

    return true
  })
}

export function CardList({
  columnId,
  cards,
  searchQuery,
  filterTagIds,
  filterUserIds,
  filterColumnIds,
  draggingCardId,
  canArchiveCards,
  selectedCardIds,
  onToggleSelectCard,
  onArchiveCard,
  onCardClick,
  onAddCard,
}: CardListProps) {
  const showCheckboxes = Boolean(selectedCardIds)
  const [scrollTop, setScrollTop] = useState(0)

  const visibleCards = useMemo(
    () => applyCardFilters(cards, searchQuery, filterTagIds, filterUserIds, filterColumnIds),
    [cards, searchQuery, filterTagIds, filterUserIds, filterColumnIds],
  )

  const sortableIds = useMemo(() => visibleCards.map((card) => `card-${card.id}`), [visibleCards])

  if (visibleCards.length === 0) {
    return (
      <SortableContext items={sortableIds} strategy={verticalListSortingStrategy}>
        <Stack alignItems="center" spacing={0.75} sx={{ py: 4 }}>
          <InboxOutlinedIcon sx={{ fontSize: 28, color: 'text.disabled' }} />
          <Typography variant="body2" color="text.disabled">
            No tasks
          </Typography>
          {onAddCard ? (
            <Typography
              variant="caption"
              color="primary"
              sx={{ cursor: 'pointer', '&:hover': { textDecoration: 'underline' } }}
              onClick={onAddCard}
            >
              <AddIcon sx={{ fontSize: 14, verticalAlign: 'middle', mr: 0.25 }} />
              Add a card
            </Typography>
          ) : null}
        </Stack>
      </SortableContext>
    )
  }

  const shouldVirtualize = visibleCards.length >= VIRTUALIZATION_THRESHOLD

  if (!shouldVirtualize) {
    return (
      <SortableContext items={sortableIds} strategy={verticalListSortingStrategy}>
        <Stack spacing={0.75}>
          {visibleCards.map((card) => (
            <MemoizedCardListItem
              key={card.id}
              columnId={columnId}
              card={card}
              draggingCardId={draggingCardId}
              canArchive={canArchiveCards}
              isSelected={selectedCardIds?.has(card.id)}
              showCheckbox={showCheckboxes}
              onToggleSelect={onToggleSelectCard}
              onArchive={onArchiveCard}
              onClick={onCardClick}
            />
          ))}
        </Stack>
      </SortableContext>
    )
  }

  const totalHeight = visibleCards.length * CARD_ITEM_HEIGHT
  const startIndex = Math.max(0, Math.floor(scrollTop / CARD_ITEM_HEIGHT) - VIRTUAL_OVERSCAN)
  const visibleCount = Math.ceil(CARD_LIST_MAX_HEIGHT / CARD_ITEM_HEIGHT) + VIRTUAL_OVERSCAN * 2
  const endIndex = Math.min(visibleCards.length, startIndex + visibleCount)
  const virtualItems = visibleCards.slice(startIndex, endIndex)
  const topPadding = startIndex * CARD_ITEM_HEIGHT

  return (
    <SortableContext items={sortableIds} strategy={verticalListSortingStrategy}>
      <Box
        sx={{ maxHeight: CARD_LIST_MAX_HEIGHT, overflowY: 'auto' }}
        onScroll={(event) => setScrollTop(event.currentTarget.scrollTop)}
      >
        <Box sx={{ height: totalHeight, position: 'relative' }}>
          <Box sx={{ position: 'absolute', top: topPadding, left: 0, right: 0 }}>
            <Stack spacing={1}>
              {virtualItems.map((card) => (
                <MemoizedCardListItem
                  key={card.id}
                  columnId={columnId}
                  card={card}
                  draggingCardId={draggingCardId}
                  canArchive={canArchiveCards}
                  onArchive={onArchiveCard}
                  onClick={onCardClick}
                />
              ))}
            </Stack>
          </Box>
        </Box>
      </Box>
    </SortableContext>
  )
}
