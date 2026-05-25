import { forwardRef, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import { alpha, useTheme } from '@mui/material/styles'
import { useDroppable } from '@dnd-kit/core'
import type { Card } from '@/lib/types'
import type { CardMeta } from '@/features/gantt/utils/ganttConstants'
import { BAR_GAP, HEADER_HEIGHT, ROW_HEIGHT, SCROLL_EXTEND_THRESHOLD, WEEKDAY_LABELS } from '@/features/gantt/utils/ganttConstants'
import { getDaysBetween, generateDays, groupDaysByMonth, hasScheduleDates } from '@/features/gantt/utils/ganttUtils'
import { GanttBar } from './GanttBar'

export type { CardMeta }

type GanttTimelineProps = {
  scheduledCards: Card[]
  timelineStart: Date
  timelineEnd: Date
  dayWidth: number
  droppableId?: string
  focusedCardId?: string | null
  cardMeta?: Record<string, CardMeta>
  readOnly?: boolean
  onBarClick?: (card: Card) => void
  onBarMove?: (cardId: string, startDate: string, dueDate: string) => void
  onBarResize?: (cardId: string, startDate: string, dueDate: string) => void
  onBarReorder?: (cardId: string, newIndex: number) => void
  onBarUnschedule?: (cardId: string) => void
  onExtendRange?: (direction: 'left' | 'right') => void
}

function TimelineDropArea({ timelineWidth, droppableId }: { timelineWidth: number; droppableId: string }) {
  const { setNodeRef, isOver } = useDroppable({
    id: droppableId,
  })

  return (
    <Box
      ref={setNodeRef}
      sx={{
        position: 'absolute',
        top: HEADER_HEIGHT,
        left: 0,
        width: timelineWidth,
        bottom: 0,
        bgcolor: isOver ? (theme) => alpha(theme.palette.primary.main, 0.04) : 'transparent',
        zIndex: 0,
      }}
    />
  )
}

/**
 * Build a CSS repeating background that paints grid lines + weekend shading
 * with a single DOM element instead of one Box per day.
 */
function useGridBackground(dayWidth: number, timelineStart: Date, weekendBg: string, gridBorderColor: string, mondayBorderColor: string) {
  return useMemo(() => {
    // What day-of-week does the timeline start on? (0=Sun … 6=Sat)
    const startDow = timelineStart.getDay()
    const weekWidth = dayWidth * 7

    // Weekend columns: Saturday (dow 6) and Sunday (dow 0)
    // Offset from the left in px within one 7-day cycle
    const satOffset = ((6 - startDow + 7) % 7) * dayWidth
    const sunOffset = ((0 - startDow + 7) % 7) * dayWidth

    // Monday's stronger border position
    const monOffset = ((1 - startDow + 7) % 7) * dayWidth

    // Build gradient stops for a single 7-day repeating tile
    const layers = [
      // Vertical grid line every dayWidth (thin, light)
      `repeating-linear-gradient(to right, ${gridBorderColor} 0px, ${gridBorderColor} 1px, transparent 1px, transparent ${dayWidth}px)`,
      // Monday stronger border
      `repeating-linear-gradient(to right, transparent 0px, transparent ${monOffset}px, ${mondayBorderColor} ${monOffset}px, ${mondayBorderColor} ${monOffset + 1}px, transparent ${monOffset + 1}px, transparent ${weekWidth}px)`,
      // Saturday weekend shading
      `repeating-linear-gradient(to right, transparent 0px, transparent ${satOffset}px, ${weekendBg} ${satOffset}px, ${weekendBg} ${satOffset + dayWidth}px, transparent ${satOffset + dayWidth}px, transparent ${weekWidth}px)`,
      // Sunday weekend shading
      `repeating-linear-gradient(to right, transparent 0px, transparent ${sunOffset}px, ${weekendBg} ${sunOffset}px, ${weekendBg} ${sunOffset + dayWidth}px, transparent ${sunOffset + dayWidth}px, transparent ${weekWidth}px)`,
    ]

    return layers.join(', ')
  }, [dayWidth, timelineStart, weekendBg, gridBorderColor, mondayBorderColor])
}

/** Virtualized day header: only renders the visible range plus a small buffer. */
function VirtualDayHeader({
  days,
  dayWidth,
  today,
  weekendBg,
  mondayBorderColor,
  gridBorderColor,
  scrollLeft,
  viewportWidth,
}: {
  days: Date[]
  dayWidth: number
  today: Date
  weekendBg: string
  mondayBorderColor: string
  gridBorderColor: string
  scrollLeft: number
  viewportWidth: number
}) {
  const BUFFER = 5
  const startIdx = Math.max(0, Math.floor(scrollLeft / dayWidth) - BUFFER)
  const endIdx = Math.min(days.length - 1, Math.ceil((scrollLeft + viewportWidth) / dayWidth) + BUFFER)

  const visibleDays = useMemo(() => {
    const result: Array<{ day: Date; index: number }> = []
    for (let i = startIdx; i <= endIdx; i++) {
      result.push({ day: days[i], index: i })
    }
    return result
  }, [days, startIdx, endIdx])

  return (
    <Box sx={{ position: 'relative', flex: 1, width: days.length * dayWidth }}>
      {visibleDays.map(({ day, index }) => {
        const isWeekend = day.getDay() === 0 || day.getDay() === 6
        const isMonday = day.getDay() === 1
        const isToday = day.getTime() === today.getTime()
        return (
          <Box
            key={index}
            sx={{
              position: 'absolute',
              left: index * dayWidth,
              top: 0,
              bottom: 0,
              width: dayWidth,
              borderLeft: 1,
              borderColor: isMonday ? mondayBorderColor : gridBorderColor,
              boxSizing: 'border-box',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '1px',
              bgcolor: isWeekend ? weekendBg : 'transparent',
            }}
          >
            <Typography
              variant="caption"
              sx={{
                fontSize: '0.5rem',
                fontWeight: 500,
                color: isWeekend ? 'text.disabled' : 'text.secondary',
                lineHeight: 1,
              }}
            >
              {WEEKDAY_LABELS[day.getDay()]}
            </Typography>
            <Box
              sx={{
                width: 22,
                height: 22,
                borderRadius: '50%',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                bgcolor: isToday ? 'primary.main' : 'transparent',
              }}
            >
              <Typography
                variant="caption"
                sx={{
                  fontSize: '0.625rem',
                  color: isToday ? 'primary.contrastText' : isWeekend ? 'text.disabled' : 'text.primary',
                  fontWeight: isToday ? 700 : 500,
                }}
              >
                {day.getDate()}
              </Typography>
            </Box>
          </Box>
        )
      })}
    </Box>
  )
}

export const GanttTimeline = forwardRef<HTMLDivElement, GanttTimelineProps>(function GanttTimeline(
  { scheduledCards, timelineStart, timelineEnd, dayWidth, droppableId = 'gantt-timeline', focusedCardId, cardMeta, readOnly, onBarClick, onBarMove, onBarResize, onBarReorder, onBarUnschedule, onExtendRange },
  ref,
) {
  const theme = useTheme()
  const containerRef = useRef<HTMLDivElement>(null)

  const setRefs = useCallback(
    (node: HTMLDivElement | null) => {
      (containerRef as React.MutableRefObject<HTMLDivElement | null>).current = node
      if (typeof ref === 'function') ref(node)
      else if (ref) (ref as React.MutableRefObject<HTMLDivElement | null>).current = node
    },
    [ref],
  )

  const days = useMemo(() => generateDays(timelineStart, timelineEnd), [timelineStart, timelineEnd])
  const monthGroups = useMemo(() => groupDaysByMonth(days), [days])

  const timelineWidth = days.length * dayWidth

  // Memoize today so it stays stable across renders and is safe in dep arrays.
  // Only recalculates on remount, which is fine for a single session day.
  const today = useMemo(() => {
    const d = new Date()
    d.setHours(0, 0, 0, 0)
    return d
  }, [])

  const todayOffset = getDaysBetween(timelineStart, today)
  const todayX = todayOffset * dayWidth + dayWidth / 2

  // Scroll to today on initial mount, and preserve position when range extends left
  const hasScrolledRef = useRef(false)
  const prevTimelineStartRef = useRef(timelineStart.getTime())
  const timelineStartMs = timelineStart.getTime()

  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    if (!hasScrolledRef.current) {
      if (todayX > 0 && todayX < timelineWidth) {
        el.scrollLeft = Math.max(0, todayX - el.clientWidth / 2)
        hasScrolledRef.current = true
      }
    } else {
      const prevStart = prevTimelineStartRef.current
      if (timelineStartMs < prevStart) {
        const addedDays = Math.round((prevStart - timelineStartMs) / (1000 * 60 * 60 * 24))
        el.scrollLeft += addedDays * dayWidth
      }
    }
    prevTimelineStartRef.current = timelineStartMs
  }, [timelineStartMs, timelineWidth, todayX, dayWidth])

  const extendCooldown = useRef(false)
  const cooldownTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    const el = containerRef.current
    if (!el || !onExtendRange) return

    const handleScroll = () => {
      if (extendCooldown.current) return
      if (el.hasAttribute('data-bar-dragging')) return
      const { scrollLeft, scrollWidth, clientWidth } = el
      if (scrollLeft < SCROLL_EXTEND_THRESHOLD) {
        extendCooldown.current = true
        onExtendRange('left')
        cooldownTimeoutRef.current = setTimeout(() => { extendCooldown.current = false }, 500)
      } else if (scrollLeft + clientWidth > scrollWidth - SCROLL_EXTEND_THRESHOLD) {
        extendCooldown.current = true
        onExtendRange('right')
        cooldownTimeoutRef.current = setTimeout(() => { extendCooldown.current = false }, 500)
      }
    }
    el.addEventListener('scroll', handleScroll, { passive: true })
    return () => {
      el.removeEventListener('scroll', handleScroll)
      if (cooldownTimeoutRef.current !== null) {
        clearTimeout(cooldownTimeoutRef.current)
        cooldownTimeoutRef.current = null
      }
      extendCooldown.current = false
    }
  }, [onExtendRange])

  const bars = useMemo(() => {
    return scheduledCards
      .filter(hasScheduleDates)
      .map((card, index) => {
        const start = new Date(card.startDate)
        const end = new Date(card.dueDate)
        start.setHours(0, 0, 0, 0)
        end.setHours(0, 0, 0, 0)

        const startOffset = getDaysBetween(timelineStart, start)
        const duration = getDaysBetween(start, end) + 1

        const meta = cardMeta?.[card.id]
        const isOverdue = Boolean(end < today && meta && !meta.isLastColumn)

        return {
          card,
          left: startOffset * dayWidth,
          width: duration * dayWidth,
          top: HEADER_HEIGHT + index * ROW_HEIGHT + BAR_GAP,
          meta,
          isOverdue,
        }
      })
  }, [scheduledCards, timelineStart, dayWidth, cardMeta, today])

  const timelineHeight = HEADER_HEIGHT + Math.max(bars.length * ROW_HEIGHT + 200, 400)

  const weekendBg = useMemo(() => alpha(theme.palette.text.primary, 0.04), [theme.palette.text.primary])
  const mondayBorderColor = theme.palette.divider
  const gridBorderColor = useMemo(() => alpha(theme.palette.divider, 0.3), [theme.palette.divider])
  const rowHoverBg = useMemo(() => alpha(theme.palette.text.primary, 0.02), [theme.palette.text.primary])

  const gridBackground = useGridBackground(dayWidth, timelineStart, weekendBg, gridBorderColor, mondayBorderColor)

  // Track scroll position for header virtualization (updated via rAF for perf)
  const [scrollState, setScrollState] = useState({ scrollLeft: 0, viewportWidth: 1200 })
  const rafRef = useRef<number>(0)

  useEffect(() => {
    const el = containerRef.current
    if (!el) return

    const syncScroll = () => {
      setScrollState({ scrollLeft: el.scrollLeft, viewportWidth: el.clientWidth })
    }
    // Initial sync
    syncScroll()

    const handleScroll = () => {
      cancelAnimationFrame(rafRef.current)
      rafRef.current = requestAnimationFrame(syncScroll)
    }
    el.addEventListener('scroll', handleScroll, { passive: true })
    return () => {
      el.removeEventListener('scroll', handleScroll)
      cancelAnimationFrame(rafRef.current)
    }
  }, [])

  return (
    <Box
      ref={setRefs}
      data-gantt-scroll
      sx={{
        flex: 1,
        overflow: 'auto',
        position: 'relative',
        bgcolor: 'background.paper',
      }}
    >
      <Box
        sx={{
          position: 'relative',
          minWidth: timelineWidth,
          minHeight: `max(${timelineHeight}px, 100%)`,
        }}
      >
        {/* Header: month row + virtualized day row */}
        <Box
          sx={{
            position: 'sticky',
            top: 0,
            zIndex: 10,
            bgcolor: 'background.paper',
            borderBottom: 1,
            borderColor: 'divider',
            height: HEADER_HEIGHT,
            display: 'flex',
            flexDirection: 'column',
          }}
        >
          {/* Month row */}
          <Box sx={{ display: 'flex', height: 24 }}>
            {monthGroups.map((group) => (
              <Box
                key={group.label}
                sx={{
                  width: group.count * dayWidth,
                  minWidth: group.count * dayWidth,
                  borderLeft: 1,
                  borderBottom: 1,
                  borderColor: 'divider',
                  boxSizing: 'border-box',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <Typography
                  variant="caption"
                  sx={{ fontSize: '0.625rem', fontWeight: 600, color: 'text.secondary' }}
                >
                  {group.label}
                </Typography>
              </Box>
            ))}
          </Box>

          {/* Virtualized day row */}
          <VirtualDayHeader
            days={days}
            dayWidth={dayWidth}
            today={today}
            weekendBg={weekendBg}
            mondayBorderColor={mondayBorderColor}
            gridBorderColor={gridBorderColor}
            scrollLeft={scrollState.scrollLeft}
            viewportWidth={scrollState.viewportWidth}
          />
        </Box>

        {/* Grid lines + weekend shading via CSS background (single DOM element) */}
        <Box
          sx={{
            position: 'absolute',
            top: HEADER_HEIGHT,
            left: 0,
            width: timelineWidth,
            bottom: 0,
            background: gridBackground,
            zIndex: 0,
            pointerEvents: 'none',
          }}
        />

        {/* Row hover highlights via CSS-only pseudo-elements on a single container */}
        {bars.length > 0 && (
          <Box
            sx={{
              position: 'absolute',
              top: HEADER_HEIGHT,
              left: 0,
              width: timelineWidth,
              height: bars.length * ROW_HEIGHT,
              zIndex: 1,
              pointerEvents: 'none',
              /* Stripe pattern for hover rows: each "row" is ROW_HEIGHT tall.
                 We render invisible divs and let CSS handle hover. We use a
                 background-size trick so each row is individually hoverable. */
              display: 'grid',
              gridTemplateRows: `repeat(${bars.length}, ${ROW_HEIGHT}px)`,
              '& > div:hover': {
                bgcolor: rowHoverBg,
              },
            }}
          >
            {bars.map((bar) => (
              <Box key={`row-${bar.card.id}`} sx={{ pointerEvents: 'auto' }} />
            ))}
          </Box>
        )}

        {/* Drop area */}
        <TimelineDropArea timelineWidth={timelineWidth} droppableId={droppableId} />

        {/* Today line */}
        {todayX > 0 && todayX < timelineWidth && (
          <Box
            sx={{
              position: 'absolute',
              top: 0,
              left: todayX,
              width: 0,
              bottom: 0,
              borderLeft: '2px dashed',
              borderColor: 'primary.main',
              zIndex: 0,
              pointerEvents: 'none',
            }}
          />
        )}

        {/* Gantt bars */}
        {bars.map((bar, index) => (
          <GanttBar
            key={bar.card.id}
            card={bar.card}
            left={bar.left}
            width={bar.width}
            top={bar.top}
            rowIndex={index}
            totalRows={bars.length}
            dayWidth={dayWidth}
            isFocused={focusedCardId === bar.card.id}
            meta={bar.meta}
            isOverdue={bar.isOverdue}
            readOnly={readOnly}
            onBarClick={onBarClick}
            onBarMove={onBarMove}
            onBarResize={onBarResize}
            onBarReorder={onBarReorder}
            onBarUnschedule={onBarUnschedule}
            containerRef={containerRef}
          />
        ))}

        {/* Empty state */}
        {bars.length === 0 && (
          <Box
            sx={{
              position: 'absolute',
              top: HEADER_HEIGHT + 60,
              left: '50%',
              transform: 'translateX(-50%)',
              textAlign: 'center',
              zIndex: 3,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 1,
              p: 3,
              borderRadius: 2,
              border: '2px dashed',
              borderColor: 'divider',
              bgcolor: (t) => alpha(t.palette.background.paper, 0.8),
            }}
          >
            <DragIndicatorIcon sx={{ fontSize: 32, color: 'text.disabled' }} />
            {readOnly ? (
              <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 500 }}>
                No tasks are scheduled on this timeline yet
              </Typography>
            ) : (
              <>
                <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 500 }}>
                  Drag tasks from the sidebar onto the timeline to schedule them
                </Typography>
                <Typography variant="caption" color="text.disabled">
                  Drag bars off the timeline to unschedule
                </Typography>
              </>
            )}
          </Box>
        )}
      </Box>
    </Box>
  )
})
