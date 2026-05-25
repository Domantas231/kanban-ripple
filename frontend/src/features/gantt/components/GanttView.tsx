import { useCallback, useMemo, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import CalendarMonthOutlinedIcon from '@mui/icons-material/CalendarMonthOutlined'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import { DndContext, DragOverlay, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent, Modifier } from '@dnd-kit/core'
import { useScheduleCard } from '@/features/cards'
import { useProject, useProjectMembers, useSwimlaneView } from '@/features/projects'
import { useAuthStore } from '@/features/auth'
import type { BoardSwimlane, Card, Guid, ProjectRole } from '@/lib/types'
import { EmptyState } from '@/components/feedback/EmptyState'
import { GanttSkeleton } from '@/components/loading/GanttSkeleton'
import { CardDetailDialog } from '@/features/cards'
import { GanttSidebar, SidebarCardOverlay } from './GanttSidebar'
import { GanttTimeline } from './GanttTimeline'
import type { CardMeta } from './GanttTimeline'
import { GanttToolbar, ZOOM_LEVELS, DEFAULT_ZOOM_INDEX } from './GanttToolbar'

type GanttViewProps = {
  projectId: Guid
}

const BUFFER_DAYS = 90
const EXTEND_DAYS = 60
const DEFAULT_DURATION_DAYS = 3
const TIMELINE_DROPPABLE_ID = 'gantt-timeline'

function computeTimelineRange(
  boards: BoardSwimlane[],
  extraStartDays: number,
  extraEndDays: number,
): { start: Date; end: Date } {
  const today = new Date()
  today.setHours(0, 0, 0, 0)

  let minDate = new Date(today)
  let maxDate = new Date(today)

  for (const board of boards) {
    for (const col of board.columns) {
      for (const card of col.cards) {
        if (card.startDate) {
          const s = new Date(card.startDate)
          if (s < minDate) minDate = s
        }
        if (card.dueDate) {
          const d = new Date(card.dueDate)
          if (d > maxDate) maxDate = d
        }
      }
    }
  }

  const start = new Date(minDate)
  start.setDate(start.getDate() - BUFFER_DAYS - extraStartDays)
  start.setHours(0, 0, 0, 0)

  const end = new Date(maxDate)
  end.setDate(end.getDate() + BUFFER_DAYS + extraEndDays)
  end.setHours(0, 0, 0, 0)

  return { start, end }
}

export function GanttView({ projectId }: GanttViewProps) {
  const swimlaneQuery = useSwimlaneView(projectId)
  const projectQuery = useProject(projectId)
  const membersQuery = useProjectMembers(projectId)
  const scheduleCardMutation = useScheduleCard()

  const currentUserId = useAuthStore((state) => state.user?.id)

  const project = projectQuery.data
  const members = useMemo(() => membersQuery.data ?? [], [membersQuery.data])

  const currentUserRole = useMemo(() => {
    if (!currentUserId) return undefined
    if (project?.ownerId === currentUserId) return 0 as ProjectRole
    return members.find((member) => member.userId === currentUserId)?.role
  }, [currentUserId, members, project?.ownerId])

  const canManageCards = currentUserRole !== undefined && currentUserRole <= 2

  const [selectedCardId, setSelectedCardId] = useState<Guid | null>(null)
  const [focusedCardId, setFocusedCardId] = useState<Guid | null>(null)
  const zoomIndex = DEFAULT_ZOOM_INDEX
  const [extraStart, setExtraStart] = useState(0)
  const [extraEnd, setExtraEnd] = useState(0)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    typeof window !== 'undefined' && window.innerWidth < 600,
  )
  const [cardOrder, setCardOrder] = useState<string[]>([])
  const [selectedBoardId, setSelectedBoardId] = useState<string>('')
  const [hiddenBoardIds, setHiddenBoardIds] = useState<ReadonlySet<Guid>>(() => new Set())
  const timelineScrollRef = useRef<HTMLDivElement>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 4 },
    }),
  )

  const dayWidth = ZOOM_LEVELS[zoomIndex]

  const boards = useMemo(() => swimlaneQuery.data?.boards ?? [], [swimlaneQuery.data])

  if (boards.length === 0) {
    if (selectedBoardId !== '') setSelectedBoardId('')
  } else if (!boards.some((b) => b.board.id === selectedBoardId)) {
    setSelectedBoardId(boards[0].board.id)
  }

  const { start: timelineStart, end: timelineEnd } = useMemo(
    () => computeTimelineRange(boards, extraStart, extraEnd),
    [boards, extraStart, extraEnd],
  )

  const scheduledCards = useMemo(() => {
    const all: Card[] = []
    for (const board of boards) {
      if (hiddenBoardIds.has(board.board.id)) continue
      for (const col of board.columns) {
        for (const card of col.cards) {
          if (card.startDate && card.dueDate) all.push(card)
        }
      }
    }
    if (cardOrder.length === 0) return all

    const cardMap = new Map(all.map((c) => [c.id, c]))
    const ordered: Card[] = []
    for (const id of cardOrder) {
      const card = cardMap.get(id)
      if (card) {
        ordered.push(card)
        cardMap.delete(id)
      }
    }
    for (const card of cardMap.values()) {
      ordered.push(card)
    }
    return ordered
  }, [boards, cardOrder, hiddenBoardIds])

  const cardMeta = useMemo(() => {
    const meta: Record<string, CardMeta> = {}
    for (const board of boards) {
      const totalCols = board.columns.length
      board.columns.forEach((col, colIdx) => {
        for (const card of col.cards) {
          meta[card.id] = {
            columnName: col.column.name,
            columnIndex: colIdx,
            isLastColumn: colIdx === totalCols - 1,
          }
        }
      })
    }
    return meta
  }, [boards])

  const handleScrollToToday = useCallback(() => {
    const containers = document.querySelectorAll<HTMLDivElement>('[data-gantt-scroll]')
    if (containers.length === 0) return
    const today = new Date()
    today.setHours(0, 0, 0, 0)
    const diffMs = today.getTime() - timelineStart.getTime()
    const dayOffset = Math.floor(diffMs / (1000 * 60 * 60 * 24))
    const todayX = dayOffset * dayWidth + dayWidth / 2
    containers.forEach((el) => {
      el.scrollTo({ left: Math.max(0, todayX - el.clientWidth / 2), behavior: 'smooth' })
    })
  }, [timelineStart, dayWidth])

  const handleExtendRange = useCallback((direction: 'left' | 'right') => {
    if (direction === 'left') {
      setExtraStart((prev) => prev + EXTEND_DAYS)
    } else {
      setExtraEnd((prev) => prev + EXTEND_DAYS)
    }
  }, [])

  const handleBarMove = useCallback(
    (cardId: string, startDate: string, dueDate: string) => {
      if (!canManageCards) return
      setFocusedCardId(cardId)
      scheduleCardMutation.mutate({
        id: cardId,
        projectId,
        data: { startDate, dueDate },
      })
    },
    [projectId, scheduleCardMutation, canManageCards],
  )

  const handleBarResize = useCallback(
    (cardId: string, startDate: string, dueDate: string) => {
      if (!canManageCards) return
      scheduleCardMutation.mutate({
        id: cardId,
        projectId,
        data: { startDate, dueDate },
      })
    },
    [projectId, scheduleCardMutation, canManageCards],
  )

  const handleBarReorder = useCallback(
    (cardId: string, newIndex: number) => {
      const currentIds = scheduledCards.map((c) => c.id)
      const oldIndex = currentIds.indexOf(cardId)
      if (oldIndex === -1 || oldIndex === newIndex) return
      const newOrder = [...currentIds]
      newOrder.splice(oldIndex, 1)
      newOrder.splice(newIndex, 0, cardId)
      setCardOrder(newOrder)
    },
    [scheduledCards],
  )

  const handleBarUnschedule = useCallback(
    (cardId: string) => {
      if (!canManageCards) return
      scheduleCardMutation.mutate({
        id: cardId,
        projectId,
        data: { startDate: null, dueDate: null },
      })
    },
    [projectId, scheduleCardMutation, canManageCards],
  )

  const handleSidebarCardClick = useCallback((card: Card) => {
    setSelectedCardId(card.id)
    setFocusedCardId(card.id)
  }, [])

  const [activeCard, setActiveCard] = useState<Card | null>(null)

  const snapLeftEdgeToCursor: Modifier = useCallback(
    ({ activatorEvent, draggingNodeRect, transform }) => {
      if (activatorEvent instanceof PointerEvent && draggingNodeRect) {
        return {
          ...transform,
          x: transform.x + (activatorEvent.clientX - draggingNodeRect.left),
          y:
            transform.y +
            (activatorEvent.clientY - draggingNodeRect.top - draggingNodeRect.height / 2),
        }
      }
      return transform
    },
    [],
  )

  const handleDragStart = useCallback((event: DragStartEvent) => {
    const dragData = event.active.data.current
    if (dragData?.type === 'sidebar-card') {
      setActiveCard(dragData.card as Card)
    }
  }, [])

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event
      setActiveCard(null)

      if (!canManageCards) return

      const dragData = active.data.current
      if (dragData?.type !== 'sidebar-card') return
      if (!over) return

      const card = dragData.card as Card

      const timelineEl = document.querySelector('[data-gantt-timeline]')
      if (!timelineEl) {
        const startDate = new Date()
        startDate.setHours(0, 0, 0, 0)
        const dueDate = new Date(startDate)
        dueDate.setDate(dueDate.getDate() + DEFAULT_DURATION_DAYS - 1)

        scheduleCardMutation.mutate({
          id: card.id,
          projectId,
          data: {
            startDate: startDate.toISOString(),
            dueDate: dueDate.toISOString(),
          },
        })
        return
      }

      const scrollContainer = timelineEl.querySelector('[data-gantt-scroll]') ?? timelineEl
      const rect = scrollContainer.getBoundingClientRect()
      const scrollLeft = scrollContainer.scrollLeft
      // Overlay's left edge is snapped to the cursor, so its translated.left is the cursor X.
      const translated = active.rect.current.translated
      const cursorX = translated
        ? translated.left
        : event.activatorEvent instanceof PointerEvent
          ? event.activatorEvent.clientX + event.delta.x
          : 0
      const dropX = cursorX - rect.left + scrollLeft
      const dayOffset = Math.floor(dropX / dayWidth)
      const startDate = new Date(timelineStart)
      startDate.setDate(startDate.getDate() + Math.max(0, dayOffset))

      const dueDate = new Date(startDate)
      dueDate.setDate(dueDate.getDate() + DEFAULT_DURATION_DAYS - 1)

      scheduleCardMutation.mutate({
        id: card.id,
        projectId,
        data: {
          startDate: startDate.toISOString(),
          dueDate: dueDate.toISOString(),
        },
      })
    },
    [dayWidth, scheduleCardMutation, timelineStart, projectId, canManageCards],
  )

  const handleDragCancel = useCallback(() => {
    setActiveCard(null)
  }, [])

  const selectedCardBoardId = useMemo(() => {
    if (!selectedCardId) return ''
    for (const boardSwimlane of boards) {
      for (const col of boardSwimlane.columns) {
        if (col.cards.some((c) => c.id === selectedCardId)) {
          return boardSwimlane.board.id
        }
      }
    }
    return ''
  }, [selectedCardId, boards])

  const handleBarClick = useCallback(
    (card: Card) => {
      if (focusedCardId === card.id) {
        setSelectedCardId(card.id)
      } else {
        setFocusedCardId(card.id)
      }
    },
    [focusedCardId],
  )

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed((prev) => !prev)
  }, [])

  const handleToggleBoardFilter = useCallback((boardId: Guid) => {
    setHiddenBoardIds((prev) => {
      const next = new Set(prev)
      if (next.has(boardId)) {
        next.delete(boardId)
      } else {
        next.add(boardId)
      }
      return next
    })
  }, [])

  const boardFilterOptions = useMemo(
    () => boards.map((b) => ({ id: b.board.id, name: b.board.name })),
    [boards],
  )

  if (swimlaneQuery.isLoading) {
    return <GanttSkeleton />
  }

  if (swimlaneQuery.isError) {
    return (
      <EmptyState
        icon={CalendarMonthOutlinedIcon}
        title="Unable to load timeline"
        description="Something went wrong loading the project data. Please try again."
        compact
      />
    )
  }

  if (boards.length === 0) {
    return (
      <EmptyState
        icon={ViewColumnOutlinedIcon}
        title="Nothing scheduled"
        description="Create lists and add due dates to tasks to see them on the timeline."
        compact
      />
    )
  }

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          height: { xs: 'calc(100dvh - 300px)', sm: 'calc(100dvh - 260px)' },
          border: 1,
          borderColor: 'divider',
          borderRadius: 1,
          overflow: 'hidden',
        }}
      >
        <GanttToolbar
          onScrollToToday={handleScrollToToday}
          boards={boardFilterOptions}
          hiddenBoardIds={hiddenBoardIds}
          onToggleBoardFilter={handleToggleBoardFilter}
        />

        <Box
          sx={{
            flex: 1,
            display: 'flex',
            minHeight: 0,
          }}
        >
          <GanttSidebar
            boards={boards}
            collapsed={sidebarCollapsed}
            onToggleCollapse={toggleSidebar}
            onUnschedule={canManageCards ? handleBarUnschedule : undefined}
            onCardClick={handleSidebarCardClick}
            selectedBoardId={selectedBoardId}
            onSelectBoard={setSelectedBoardId}
            canEdit={canManageCards}
          />

          <Box
            sx={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}
            data-gantt-timeline
          >
            <GanttTimeline
              ref={timelineScrollRef}
              scheduledCards={scheduledCards}
              timelineStart={timelineStart}
              timelineEnd={timelineEnd}
              dayWidth={dayWidth}
              droppableId={TIMELINE_DROPPABLE_ID}
              focusedCardId={focusedCardId}
              cardMeta={cardMeta}
              readOnly={!canManageCards}
              onBarClick={handleBarClick}
              onBarMove={canManageCards ? handleBarMove : undefined}
              onBarResize={canManageCards ? handleBarResize : undefined}
              onBarReorder={canManageCards ? handleBarReorder : undefined}
              onBarUnschedule={canManageCards ? handleBarUnschedule : undefined}
              onExtendRange={handleExtendRange}
            />
          </Box>
        </Box>
      </Box>

      <DragOverlay dropAnimation={null} modifiers={[snapLeftEdgeToCursor]}>
        {activeCard ? (
          <SidebarCardOverlay card={activeCard} width={DEFAULT_DURATION_DAYS * dayWidth} />
        ) : null}
      </DragOverlay>

      <CardDetailDialog
        open={Boolean(selectedCardId)}
        cardId={selectedCardId}
        boardId={selectedCardBoardId}
        members={members}
        canManageCards={canManageCards}
        onClose={() => {
          setSelectedCardId(null)
          setFocusedCardId(null)
        }}
      />
    </DndContext>
  )
}
