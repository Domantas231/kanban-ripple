import {
  useState,
  useCallback,
  useImperativeHandle,
  forwardRef,
  useRef,
  useEffect,
  useMemo,
} from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useDroppable } from '@dnd-kit/core'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Typography from '@mui/material/Typography'
import Tooltip from '@mui/material/Tooltip'
import CloseIcon from '@mui/icons-material/Close'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import { plannerQueryKeys } from '@/features/planner/api/query-keys'
import { projectsQueryKeys } from '@/features/projects'
import { boardsQueryKeys } from '@/features/boards'
import { cardsQueryKeys } from '@/features/cards'
import { getPlannedBlocks, createBlock, updateBlock, deleteBlock } from '@/features/planner/api/planner'
import { getGoogleStatus } from '@/features/planner/api/google'
import { PlannerDayNavigation } from './PlannerDayNavigation'
import { PlannerWeekStrip } from './PlannerWeekStrip'
import { PlannerGoogleEvents } from './PlannerGoogleEvents'
import { PlannerTimeBlock } from './PlannerTimeBlock'
import {
  HOUR_LABELS,
  SLOT_HEIGHT_PX,
  TIMELINE_HEIGHT_PX,
  TOTAL_HOURS,
  formatDateParam,
  getBrowserTimeZone,
  yToTime,
  currentTimeY,
  computeOverlapLayout,
} from '@/features/planner/utils/plannerUtils'
import type { Guid, PlannedBlock } from '@/lib/types'

export const BOARD_PLANNER_PANEL_WIDTH = 340
export const PLANNER_DROP_ID = 'board-planner-timeline'
const GUTTER_WIDTH = 48
const DEFAULT_BLOCK_DURATION_MINUTES = 60

export type BoardPlannerPanelHandle = {
  handleCardDrop: (cardId: string, cardTitle: string, translatedTop: number) => void
  handleBlockMove: (blockId: string, translatedTop: number) => void
  getDropAreaRect: () => DOMRect | null
}

type DropPreview = {
  top: number
  height: number
}

type BoardPlannerPanelProps = {
  projectId: Guid
  onClose: () => void
  dropPreview?: DropPreview | null
  onBlockClick?: (cardId: string) => void
}

export const BoardPlannerPanel = forwardRef<BoardPlannerPanelHandle, BoardPlannerPanelProps>(
  function BoardPlannerPanel({ projectId, onClose, dropPreview, onBlockClick }, ref) {
    const [selectedDate, setSelectedDate] = useState(() => new Date())
    const dateParam = formatDateParam(selectedDate)
    const queryClient = useQueryClient()
    const droppableAreaRef = useRef<HTMLDivElement | null>(null)
    const scrollContainerRef = useRef<HTMLDivElement>(null)

    const blocksQuery = useQuery({
      queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam),
      queryFn: () => getPlannedBlocks(projectId, dateParam),
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
        const prevBlocks = queryClient.getQueryData<PlannedBlock[]>(
          plannerQueryKeys.plannerBlocks(projectId, dateParam),
        )

        const optimisticBlock: PlannedBlock = {
          id: `temp-${Date.now()}`,
          cardId: data.cardId,
          cardTitle: 'Scheduling...',
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
        return { prevBlocks }
      },
      onError: (_err, _data, context) => {
        if (context?.prevBlocks) {
          queryClient.setQueryData(
            plannerQueryKeys.plannerBlocks(projectId, dateParam),
            context.prevBlocks,
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
      onError: (_err, _data, context) => {
        if (context?.prevBlocks) {
          queryClient.setQueryData(
            plannerQueryKeys.plannerBlocks(projectId, dateParam),
            context.prevBlocks,
          )
        }
      },
      onSettled: (data) => invalidatePlanner(data?.cardId),
    })

    const deleteMutation = useMutation({
      mutationFn: ({ blockId }: { blockId: string; cardId?: Guid }) =>
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
      onError: (_err, _data, context) => {
        if (context?.prevBlocks) {
          queryClient.setQueryData(
            plannerQueryKeys.plannerBlocks(projectId, dateParam),
            context.prevBlocks,
          )
        }
      },
      onSettled: (_data, _err, variables) => invalidatePlanner(variables.cardId),
    })

    const handleDeleteBlock = useCallback(
      (blockId: string) => {
        const block = blocksQuery.data?.find((b) => b.id === blockId)
        deleteMutation.mutate({ blockId, cardId: block?.cardId })
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

    useEffect(() => {
      const y = currentTimeY()
      if (y !== null && scrollContainerRef.current) {
        scrollContainerRef.current.scrollTop = Math.max(0, y - 120)
      }
    }, [])

    const isToday = dateParam === formatDateParam(new Date())
    const [nowY, setNowY] = useState<number | null>(isToday ? currentTimeY() : null)
    const [prevIsToday, setPrevIsToday] = useState(isToday)

    if (isToday !== prevIsToday) {
      setPrevIsToday(isToday)
      setNowY(isToday ? currentTimeY() : null)
    }

    useEffect(() => {
      if (!isToday) return
      const interval = setInterval(() => setNowY(currentTimeY()), 30_000)
      return () => clearInterval(interval)
    }, [isToday])

    useImperativeHandle(
      ref,
      () => ({
        handleCardDrop(cardId: string, _cardTitle: string, translatedTop: number) {
          const areaEl = droppableAreaRef.current
          if (!areaEl) return

          const areaRect = areaEl.getBoundingClientRect()
          const relativeY = translatedTop - areaRect.top
          const startTime = yToTime(Math.max(0, relativeY))
          const endTime = addMinutes(startTime, DEFAULT_BLOCK_DURATION_MINUTES)

          createMutation.mutate({
            cardId,
            date: dateParam,
            startTime,
            endTime,
            timeZone: getBrowserTimeZone(),
          })
        },
        handleBlockMove(blockId: string, translatedTop: number) {
          const areaEl = droppableAreaRef.current
          if (!areaEl) return

          const blocks = blocksQuery.data ?? []
          const block = blocks.find((b) => b.id === blockId)
          if (!block) return

          const areaRect = areaEl.getBoundingClientRect()
          const relativeY = translatedTop - areaRect.top
          const startTime = yToTime(Math.max(0, relativeY))
          const durationMinutes = timeDiffMinutes(block.startTime, block.endTime)
          const endTime = addMinutes(startTime, durationMinutes)

          if (startTime !== block.startTime) {
            updateMutation.mutate({ blockId, startTime, endTime, timeZone: getBrowserTimeZone() })
          }
        },
        getDropAreaRect() {
          return droppableAreaRef.current?.getBoundingClientRect() ?? null
        },
      }),
      [dateParam, createMutation, updateMutation, blocksQuery.data],
    )

    const { setNodeRef: setDroppableRef, isOver } = useDroppable({
      id: PLANNER_DROP_ID,
      data: { type: 'planner-timeline' },
    })

    const setDropAreaRefs = useCallback(
      (node: HTMLDivElement | null) => {
        droppableAreaRef.current = node
        setDroppableRef(node)
      },
      [setDroppableRef],
    )

    const blocks = blocksQuery.data ?? []
    const overlapLayout = useMemo(() => computeOverlapLayout(blocks), [blocks])

    return (
      <Box
        sx={{
          width: '100%',
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
          bgcolor: 'background.default',
        }}
      >
        {/* Header */}
        <Box
          sx={{
            px: 1.5,
            py: 1,
            borderBottom: 1,
            borderColor: 'divider',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Typography
            variant="body2"
            sx={{ fontWeight: 700, letterSpacing: 0.5, fontSize: '0.75rem' }}
          >
            Google Calendar
          </Typography>
          <Tooltip title="Close planner">
            <IconButton size="small" onClick={onClose} aria-label="Close planner panel">
              <CloseIcon sx={{ fontSize: 16 }} />
            </IconButton>
          </Tooltip>
        </Box>

        {/* Day navigation (compact) */}
        <Box sx={{ px: 1, py: 0.5, borderBottom: 1, borderColor: 'divider' }}>
          <PlannerDayNavigation
            selectedDate={selectedDate}
            onDateChange={handleDateChange}
            compact
          />
        </Box>

        {/* Week overview strip */}
        <Box sx={{ px: 1, py: 0.5, borderBottom: 1, borderColor: 'divider' }}>
          <PlannerWeekStrip
            projectId={projectId}
            selectedDate={selectedDate}
            onDateChange={handleDateChange}
          />
        </Box>

        {/* Scrollable timeline */}
        <Box ref={scrollContainerRef} sx={{ flex: 1, overflow: 'auto', px: 0.5, py: 1 }}>
          <Box
            sx={{
              position: 'relative',
              height: TIMELINE_HEIGHT_PX,
              minWidth: 0,
            }}
          >
            {/* Hour grid lines + labels */}
            {HOUR_LABELS.map((label, i) => {
              if (i > TOTAL_HOURS) return null
              const top = i * SLOT_HEIGHT_PX
              return (
                <Box key={label} sx={{ position: 'absolute', top, left: 0, right: 0 }}>
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{
                      position: 'absolute',
                      top: -8,
                      left: 0,
                      width: GUTTER_WIDTH,
                      textAlign: 'right',
                      pr: 1,
                      fontSize: '0.625rem',
                      userSelect: 'none',
                    }}
                  >
                    {label}
                  </Typography>
                  <Box
                    sx={{
                      position: 'absolute',
                      left: GUTTER_WIDTH,
                      right: 0,
                      height: '1px',
                      bgcolor: 'divider',
                    }}
                  />
                </Box>
              )
            })}

            {/* 15-min sub-grid lines */}
            {Array.from({ length: TOTAL_HOURS * 4 }, (_, i) => {
              if (i % 4 === 0) return null
              const top = i * (SLOT_HEIGHT_PX / 4)
              return (
                <Box
                  key={`sub-${i}`}
                  sx={{
                    position: 'absolute',
                    top,
                    left: GUTTER_WIDTH,
                    right: 0,
                    height: '1px',
                    bgcolor: 'divider',
                    opacity: 0.3,
                  }}
                />
              )
            })}

            {/* Current time indicator */}
            {nowY !== null && (
              <Box
                sx={{
                  position: 'absolute',
                  top: nowY,
                  left: GUTTER_WIDTH - 4,
                  right: 0,
                  zIndex: 3,
                  pointerEvents: 'none',
                }}
              >
                <Box
                  sx={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    bgcolor: 'error.main',
                    position: 'absolute',
                    top: -2.5,
                    left: 0,
                  }}
                />
                <Box
                  sx={{
                    position: 'absolute',
                    top: 0,
                    left: 6,
                    right: 0,
                    height: '2px',
                    bgcolor: 'error.main',
                  }}
                />
              </Box>
            )}

            {/* Droppable blocks area */}
            <Box
              ref={setDropAreaRefs}
              sx={{
                position: 'absolute',
                top: 0,
                left: GUTTER_WIDTH,
                right: 0,
                height: '100%',
                bgcolor: isOver ? 'action.hover' : 'transparent',
                borderRadius: 1,
              }}
            >
              {dropPreview && (
                <Box
                  sx={{
                    position: 'absolute',
                    top: dropPreview.top,
                    left: 4,
                    right: 4,
                    height: dropPreview.height,
                    bgcolor: 'primary.main',
                    opacity: 0.15,
                    borderRadius: 1,
                    border: 2,
                    borderColor: 'primary.main',
                    borderStyle: 'solid',
                    pointerEvents: 'none',
                  }}
                />
              )}
              <PlannerGoogleEvents date={dateParam} isConnected={googleConnected} />

              {/* Empty state */}
              {blocks.length === 0 && !dropPreview && (
                <Box
                  sx={{
                    position: 'absolute',
                    top: '50%',
                    left: '50%',
                    transform: 'translate(-50%, -50%)',
                    textAlign: 'center',
                    pointerEvents: 'none',
                    opacity: 0.5,
                  }}
                >
                  <DragIndicatorIcon sx={{ fontSize: 28, color: 'text.disabled', mb: 0.5 }} />
                  <Typography variant="caption" color="text.disabled">
                    Drag cards here
                  </Typography>
                </Box>
              )}

              {blocks.map((block) => {
                const layout = overlapLayout.get(block.id)
                return (
                  <PlannerTimeBlock
                    key={block.id}
                    block={block}
                    onDelete={handleDeleteBlock}
                    onResize={handleResizeBlock}
                    onClick={onBlockClick}
                    overlapColumn={layout?.column}
                    overlapTotalColumns={layout?.totalColumns}
                  />
                )
              })}
            </Box>
          </Box>
        </Box>
      </Box>
    )
  },
)

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
