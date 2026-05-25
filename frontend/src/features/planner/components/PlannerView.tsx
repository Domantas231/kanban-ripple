import { useState, useCallback, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragStartEvent,
  type DragMoveEvent,
  type DragEndEvent,
} from '@dnd-kit/core'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useUiStore } from '@/stores/uiStore'
import { plannerQueryKeys } from '@/features/planner/api/query-keys'
import { projectsQueryKeys } from '@/features/projects'
import { boardsQueryKeys } from '@/features/boards'
import { cardsQueryKeys } from '@/features/cards'
import {
  getPlannedBlocks,
  getUnscheduledCards,
  createBlock,
  updateBlock,
  deleteBlock,
} from '@/features/planner/api/planner'
import { getGoogleStatus } from '@/features/planner/api/google'
import { PlannerDayNavigation } from './PlannerDayNavigation'
import { PlannerWeekStrip } from './PlannerWeekStrip'
import { PlannerTimeline } from './PlannerTimeline'
import { PlannerSidebar } from './PlannerSidebar'
import { PlannerSkeleton } from './PlannerSkeleton'
import { PlannerConnectPrompt } from './PlannerConnectPrompt'
import { PlannerTimeBlock } from './PlannerTimeBlock'
import {
  formatDateParam,
  getBrowserTimeZone,
  yToTime,
  timeToY,
  blockHeight,
  TIMELINE_HEIGHT_PX,
  ROW_HEIGHT_PX,
} from '@/features/planner/utils/plannerUtils'
import type { Guid, PlannedBlock, UnscheduledCard } from '@/lib/types'

const DEFAULT_BLOCK_DURATION_MINUTES = 60

type PlannerViewProps = {
  projectId: Guid
}

type ActiveDrag =
  | { type: 'sidebar-card'; card: UnscheduledCard }
  | { type: 'block'; block: PlannedBlock }
  | null

export function PlannerView({ projectId }: PlannerViewProps) {
  const [selectedDate, setSelectedDate] = useState(() => new Date())
  const dateParam = formatDateParam(selectedDate)
  const queryClient = useQueryClient()
  const [activeDrag, setActiveDrag] = useState<ActiveDrag>(null)
  const [dropPreview, setDropPreview] = useState<{ top: number; height: number } | null>(null)
  const [snapTime, setSnapTime] = useState<string | null>(null)
  const timelineRef = useRef<HTMLDivElement>(null)
  const enqueueToast = useUiStore((state) => state.enqueueToast)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }))

  const blocksQuery = useQuery({
    queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam),
    queryFn: () => getPlannedBlocks(projectId, dateParam),
  })

  const unscheduledQuery = useQuery({
    queryKey: plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
    queryFn: () => getUnscheduledCards(projectId, dateParam),
  })

  const googleStatusQuery = useQuery({
    queryKey: plannerQueryKeys.googleStatus,
    queryFn: getGoogleStatus,
    staleTime: 5 * 60 * 1000,
  })

  const googleConnected = googleStatusQuery.data?.connected ?? false

  const invalidatePlanner = useCallback(
    (cardId?: Guid) => {
      queryClient.invalidateQueries({ queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam) })
      queryClient.invalidateQueries({
        queryKey: plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
      })
      queryClient.invalidateQueries({ queryKey: plannerQueryKeys.googleCalendarEvents(dateParam) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(projectId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      if (cardId) {
        queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(cardId) })
      }
    },
    [queryClient, projectId, dateParam],
  )

  const createMutation = useMutation({
    mutationFn: (data: {
      cardId: string
      date: string
      startTime: string
      endTime: string
      timeZone: string
    }) => createBlock(projectId, data),
    onMutate: async (data) => {
      await queryClient.cancelQueries({ queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam) })
      await queryClient.cancelQueries({
        queryKey: plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
      })

      const prevBlocks = queryClient.getQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
      )
      const prevUnscheduled = queryClient.getQueryData<UnscheduledCard[]>(
        plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
      )

      const card = prevUnscheduled?.find((c) => c.id === data.cardId)
      const optimisticBlock: PlannedBlock = {
        id: `temp-${Date.now()}`,
        cardId: data.cardId,
        cardTitle: card?.title ?? 'Loading...',
        projectId,
        date: data.date,
        startTime: data.startTime,
        endTime: data.endTime,
        syncStatus: 0,
        googleEventId: null,
      }

      queryClient.setQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
        (old) => [...(old ?? []), optimisticBlock],
      )
      queryClient.setQueryData<UnscheduledCard[]>(
        plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
        (old) => (old ?? []).filter((c) => c.id !== data.cardId),
      )

      return { prevBlocks, prevUnscheduled }
    },
    onSuccess: (data) => {
      if (data.syncStatus === 1) {
        enqueueToast({ message: 'Synced with Google Calendar', severity: 'success' })
      } else if (data.syncStatus === 2) {
        enqueueToast({ message: 'Failed to sync with Google Calendar', severity: 'warning' })
      }
    },
    onError: (_err, _data, context) => {
      if (context?.prevBlocks) {
        queryClient.setQueryData(plannerQueryKeys.plannerBlocks(projectId, dateParam), context.prevBlocks)
      }
      if (context?.prevUnscheduled) {
        queryClient.setQueryData(
          plannerQueryKeys.plannerUnscheduled(projectId, dateParam),
          context.prevUnscheduled,
        )
      }
    },
    onSettled: (_data, _err, variables) => invalidatePlanner(variables.cardId),
  })

  const updateMutation = useMutation({
    mutationFn: (data: {
      blockId: string
      startTime?: string
      endTime?: string
      timeZone?: string
    }) =>
      updateBlock(projectId, data.blockId, {
        startTime: data.startTime,
        endTime: data.endTime,
        timeZone: data.timeZone,
      }),
    onMutate: async (data) => {
      await queryClient.cancelQueries({ queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam) })
      const prevBlocks = queryClient.getQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
      )

      queryClient.setQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
        (old) =>
          (old ?? []).map((b) =>
            b.id === data.blockId
              ? {
                  ...b,
                  startTime: data.startTime ?? b.startTime,
                  endTime: data.endTime ?? b.endTime,
                }
              : b,
          ),
      )

      return { prevBlocks }
    },
    onSuccess: (data) => {
      if (data.syncStatus === 1) {
        enqueueToast({ message: 'Synced with Google Calendar', severity: 'success' })
      } else if (data.syncStatus === 2) {
        enqueueToast({ message: 'Failed to sync with Google Calendar', severity: 'warning' })
      }
    },
    onError: (_err, _data, context) => {
      if (context?.prevBlocks) {
        queryClient.setQueryData(plannerQueryKeys.plannerBlocks(projectId, dateParam), context.prevBlocks)
      }
    },
    onSettled: (data) => invalidatePlanner(data?.cardId),
  })

  const deleteMutation = useMutation({
    mutationFn: ({ blockId }: { blockId: string; hadGoogleSync: boolean; cardId?: Guid }) =>
      deleteBlock(projectId, blockId),
    onMutate: async ({ blockId }) => {
      await queryClient.cancelQueries({ queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam) })
      const prevBlocks = queryClient.getQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
      )

      queryClient.setQueryData<PlannedBlock[]>(
        plannerQueryKeys.plannerBlocks(projectId, dateParam),
        (old) => (old ?? []).filter((b) => b.id !== blockId),
      )

      return { prevBlocks }
    },
    onSuccess: (_data, { hadGoogleSync }) => {
      if (hadGoogleSync) {
        enqueueToast({ message: 'Removed from Google Calendar', severity: 'success' })
      }
    },
    onError: (_err, _data, context) => {
      if (context?.prevBlocks) {
        queryClient.setQueryData(plannerQueryKeys.plannerBlocks(projectId, dateParam), context.prevBlocks)
      }
    },
    onSettled: (_data, _err, variables) => invalidatePlanner(variables.cardId),
  })

  const handleDeleteBlock = useCallback(
    (blockId: string) => {
      const blocks = blocksQuery.data ?? []
      const block = blocks.find((b) => b.id === blockId)
      const hadGoogleSync = !!block?.googleEventId
      deleteMutation.mutate({ blockId, hadGoogleSync, cardId: block?.cardId })
    },
    [blocksQuery.data, deleteMutation],
  )

  const handleResizeBlock = useCallback(
    (blockId: string, newEndTime: string) => {
      const blocks = blocksQuery.data ?? []
      const block = blocks.find((b) => b.id === blockId)
      if (!block || newEndTime === block.endTime) return
      updateMutation.mutate({
        blockId,
        startTime: block.startTime,
        endTime: newEndTime,
        timeZone: getBrowserTimeZone(),
      })
    },
    [blocksQuery.data, updateMutation],
  )

  const handleDateChange = useCallback((date: Date) => setSelectedDate(date), [])

  const handleBlockClick = useCallback(
    (cardId: string) => {
      // Navigate to the board page with cardId to open CardDetailDialog
      // For now, open in a new tab via URL
      const block = (blocksQuery.data ?? []).find((b) => b.cardId === cardId)
      if (block) {
        window.open(`/projects/${projectId}/boards/${block.cardId}?cardId=${cardId}`, '_self')
      }
    },
    [blocksQuery.data, projectId],
  )

  const handleDragStart = useCallback(
    (event: DragStartEvent) => {
      const { active } = event
      const data = active.data.current
      if (data?.type === 'sidebar-card') {
        setActiveDrag({ type: 'sidebar-card', card: data.card as UnscheduledCard })
      } else if (data?.type === 'block') {
        setActiveDrag({ type: 'block', block: data.block as PlannedBlock })
      }
    },
    [setActiveDrag],
  )

  const handleDragMove = useCallback(
    (event: DragMoveEvent) => {
      const { active, over } = event
      if (!over || over.id !== 'timeline-drop-area' || !activeDrag) {
        setDropPreview(null)
        setSnapTime(null)
        return
      }

      const dropAreaRect = over.rect
      const cursorY = (active.rect.current.translated?.top ?? 0) - dropAreaRect.top
      const snappedTime = yToTime(Math.max(0, cursorY))
      const snappedTop = timeToY(snappedTime)

      setSnapTime(snappedTime.slice(0, 5))

      let previewHeight: number
      if (activeDrag.type === 'block') {
        previewHeight = blockHeight(activeDrag.block.startTime, activeDrag.block.endTime)
      } else {
        previewHeight = (DEFAULT_BLOCK_DURATION_MINUTES / 60) * ROW_HEIGHT_PX * 4
      }

      setDropPreview({ top: snappedTop, height: Math.max(ROW_HEIGHT_PX, previewHeight) })
    },
    [activeDrag, setDropPreview, setSnapTime],
  )

  const handleDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event
      setActiveDrag(null)
      setDropPreview(null)
      setSnapTime(null)

      if (!over || over.id !== 'timeline-drop-area') return

      const data = active.data.current
      if (!data) return

      // Calculate drop Y position relative to the droppable timeline area
      const dropAreaRect = over.rect
      const cursorY = (active.rect.current.translated?.top ?? 0) - dropAreaRect.top
      const startTime = yToTime(Math.max(0, cursorY))

      if (data.type === 'sidebar-card') {
        const card = data.card as UnscheduledCard
        const endTime = addMinutes(startTime, DEFAULT_BLOCK_DURATION_MINUTES)
        createMutation.mutate({
          cardId: card.id,
          date: dateParam,
          startTime,
          endTime,
          timeZone: getBrowserTimeZone(),
        })
      } else if (data.type === 'block') {
        const block = data.block as PlannedBlock
        const durationMinutes = timeDiffMinutes(block.startTime, block.endTime)
        const endTime = addMinutes(startTime, durationMinutes)
        if (startTime !== block.startTime) {
          updateMutation.mutate({
            blockId: block.id,
            startTime,
            endTime,
            timeZone: getBrowserTimeZone(),
          })
        }
      }
    },
    [dateParam, createMutation, updateMutation, setActiveDrag, setDropPreview, setSnapTime],
  )

  const handleDragCancel = useCallback(() => {
    setActiveDrag(null)
    setDropPreview(null)
    setSnapTime(null)
  }, [setActiveDrag, setDropPreview, setSnapTime])

  const isInitialLoading = blocksQuery.isLoading || unscheduledQuery.isLoading

  if (isInitialLoading) {
    return <PlannerSkeleton />
  }

  const blocks = blocksQuery.data ?? []
  const unscheduledCards = unscheduledQuery.data ?? []
  const isToday = dateParam === formatDateParam(new Date())

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragMove={handleDragMove}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <Stack spacing={2} sx={{ height: '100%' }}>
        {!googleConnected && !googleStatusQuery.isLoading && <PlannerConnectPrompt />}
        <PlannerDayNavigation selectedDate={selectedDate} onDateChange={handleDateChange} />

        {/* Week overview strip */}
        <PlannerWeekStrip
          projectId={projectId}
          selectedDate={selectedDate}
          onDateChange={handleDateChange}
        />

        <Box
          sx={{
            display: 'flex',
            border: 1,
            borderColor: 'divider',
            borderRadius: 2,
            overflow: 'hidden',
            height: TIMELINE_HEIGHT_PX + 32,
            minHeight: 400,
          }}
        >
          <PlannerSidebar cards={unscheduledCards} isLoading={unscheduledQuery.isFetching} />

          <Box
            ref={timelineRef}
            sx={{
              flex: 1,
              overflow: 'auto',
              py: 2,
              px: 1,
              position: 'relative',
            }}
          >
            <PlannerTimeline
              blocks={blocks}
              onDeleteBlock={handleDeleteBlock}
              onResizeBlock={handleResizeBlock}
              onBlockClick={handleBlockClick}
              googleDate={dateParam}
              googleConnected={googleConnected}
              dropPreview={dropPreview}
              snapTime={snapTime}
              autoScrollRef={timelineRef}
              isToday={isToday}
            />
          </Box>
        </Box>
      </Stack>

      <DragOverlay dropAnimation={null}>
        {activeDrag?.type === 'sidebar-card' && (
          <Box
            sx={{
              p: 1.5,
              borderRadius: 1,
              border: 1,
              borderColor: 'divider',
              bgcolor: 'background.paper',
              boxShadow: 4,
              width: 240,
              cursor: 'grabbing',
            }}
          >
            <Typography
              variant="body2"
              sx={{
                fontWeight: 500,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
            >
              {activeDrag.card.title}
            </Typography>
            <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5 }}>
              <Chip
                label={activeDrag.card.boardName}
                size="small"
                variant="outlined"
                sx={{ height: 20, fontSize: '0.6875rem' }}
              />
            </Box>
          </Box>
        )}
        {activeDrag?.type === 'block' && (
          <PlannerTimeBlock block={activeDrag.block} isDragOverlay />
        )}
      </DragOverlay>
    </DndContext>
  )
}

function addMinutes(time: string, minutes: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = h * 60 + m + minutes
  const newH = Math.min(22, Math.floor(total / 60))
  const newM = newH === 22 ? 0 : total % 60
  return `${newH.toString().padStart(2, '0')}:${newM.toString().padStart(2, '0')}:00`
}

function timeDiffMinutes(start: string, end: string): number {
  const [sh, sm] = start.split(':').map(Number)
  const [eh, em] = end.split(':').map(Number)
  return eh * 60 + em - (sh * 60 + sm)
}
