import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { RefObject } from 'react'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { alpha, useTheme } from '@mui/material/styles'
import type { Card } from '@/lib/types'
import type { CardMeta } from '@/features/gantt/utils/ganttConstants'
import { BAR_HEIGHT, CLICK_THRESHOLD, HEADER_HEIGHT, ROW_HEIGHT, BAR_GAP } from '@/features/gantt/utils/ganttConstants'
import { formatDateShort } from '@/features/gantt/utils/ganttUtils'

type GanttBarProps = {
  card: Card
  left: number
  width: number
  top: number
  rowIndex: number
  totalRows: number
  dayWidth: number
  isFocused?: boolean
  meta?: CardMeta
  isOverdue?: boolean
  readOnly?: boolean
  onBarClick?: (card: Card) => void
  onBarMove?: (cardId: string, startDate: string, dueDate: string) => void
  onBarResize?: (cardId: string, startDate: string, dueDate: string) => void
  onBarReorder?: (cardId: string, newIndex: number) => void
  onBarUnschedule?: (cardId: string) => void
  containerRef?: RefObject<HTMLDivElement | null>
}

export const GanttBar = memo(function GanttBar({
  card,
  left,
  width,
  top,
  rowIndex,
  totalRows,
  dayWidth,
  isFocused,
  isOverdue,
  readOnly,
  onBarClick,
  onBarMove,
  onBarResize,
  onBarReorder,
  onBarUnschedule,
  containerRef,
}: GanttBarProps) {
  const theme = useTheme()
  const startDate = useMemo(
    () => (card.startDate ? new Date(card.startDate) : null),
    [card.startDate],
  )
  const dueDate = useMemo(() => (card.dueDate ? new Date(card.dueDate) : null), [card.dueDate])

  const [isDragging, setIsDragging] = useState(false)
  const dragState = useRef<{
    startX: number
    startY: number
    initialLeft: number
    initialTop: number
    initialScrollLeft: number
  } | null>(null)
  const lastPointerRef = useRef({ x: 0, y: 0 })
  const [currentLeft, setCurrentLeft] = useState(left)
  const [currentTop, setCurrentTop] = useState(top)

  const [resizeMode, setResizeMode] = useState<'left' | 'right' | null>(null)
  const resizeState = useRef<{ startX: number; initialLeft: number; initialWidth: number } | null>(
    null,
  )
  const [resizeLeft, setResizeLeft] = useState(left)
  const [resizeWidth, setResizeWidth] = useState(width)

  // Clear optimistic via setState-during-render (React-recommended pattern for
  // deriving state from props) instead of a useEffect, which causes a double commit.
  const [optimistic, setOptimistic] = useState<{
    left?: number
    top?: number
    width?: number
  } | null>(null)
  const [trackedProps, setTrackedProps] = useState({ left, top, width })

  if (left !== trackedProps.left || top !== trackedProps.top || width !== trackedProps.width) {
    setTrackedProps({ left, top, width })
    if (optimistic !== null) setOptimistic(null)
  }

  const isInteracting = isDragging || resizeMode !== null

  const handlePointerDown = useCallback(
    (e: React.PointerEvent<HTMLDivElement>) => {
      if (readOnly) return
      if (!startDate || !dueDate) return
      e.currentTarget.setPointerCapture(e.pointerId)
      const scrollLeft = containerRef?.current?.scrollLeft ?? 0
      dragState.current = {
        startX: e.clientX,
        startY: e.clientY,
        initialLeft: left,
        initialTop: top,
        initialScrollLeft: scrollLeft,
      }
      lastPointerRef.current = { x: e.clientX, y: e.clientY }
      setCurrentLeft(left)
      setCurrentTop(top)
      setIsDragging(true)
      containerRef?.current?.setAttribute('data-bar-dragging', 'true')
    },
    [left, top, startDate, dueDate, containerRef, readOnly],
  )

  const handlePointerMove = useCallback(
    (e: React.PointerEvent<HTMLDivElement>) => {
      if (!isDragging || !dragState.current) return
      lastPointerRef.current = { x: e.clientX, y: e.clientY }
      const deltaX = e.clientX - dragState.current.startX
      const deltaY = e.clientY - dragState.current.startY
      const scrollDelta =
        (containerRef?.current?.scrollLeft ?? 0) - dragState.current.initialScrollLeft
      setCurrentLeft(dragState.current.initialLeft + deltaX + scrollDelta)
      setCurrentTop(dragState.current.initialTop + deltaY)
    },
    [isDragging, containerRef],
  )

  const handlePointerUp = useCallback(
    (e: React.PointerEvent<HTMLDivElement>) => {
      if (!isDragging || !dragState.current || !startDate || !dueDate) {
        setIsDragging(false)
        dragState.current = null
        containerRef?.current?.removeAttribute('data-bar-dragging')
        return
      }

      const deltaX = e.clientX - dragState.current.startX
      const deltaY = e.clientY - dragState.current.startY
      const scrollDelta =
        (containerRef?.current?.scrollLeft ?? 0) - dragState.current.initialScrollLeft
      setIsDragging(false)
      dragState.current = null
      containerRef?.current?.removeAttribute('data-bar-dragging')

      const totalDisplacement = Math.sqrt(deltaX * deltaX + deltaY * deltaY)
      if (totalDisplacement < CLICK_THRESHOLD) {
        onBarClick?.(card)
        return
      }

      if (containerRef?.current && onBarUnschedule) {
        const rect = containerRef.current.getBoundingClientRect()
        if (
          e.clientX < rect.left ||
          e.clientX > rect.right ||
          e.clientY < rect.top - 40 ||
          e.clientY > rect.bottom + 40
        ) {
          onBarUnschedule(card.id)
          return
        }
      }

      const targetRow = Math.round((currentTop - HEADER_HEIGHT - BAR_GAP) / ROW_HEIGHT)
      const clampedRow = Math.max(0, Math.min(targetRow, totalRows - 1))
      const didReorder = clampedRow !== rowIndex
      if (didReorder) {
        onBarReorder?.(card.id, clampedRow)
      }

      const totalDeltaX = deltaX + scrollDelta
      const dayDelta = Math.round(totalDeltaX / dayWidth)

      if (dayDelta !== 0) {
        const newStart = new Date(startDate)
        newStart.setDate(newStart.getDate() + dayDelta)
        const newEnd = new Date(dueDate)
        newEnd.setDate(newEnd.getDate() + dayDelta)
        const snappedDelta = dayDelta * dayWidth
        setOptimistic({ left: left + snappedDelta, top: didReorder ? undefined : top })
        onBarMove?.(card.id, newStart.toISOString(), newEnd.toISOString())
      } else if (didReorder) {
        setOptimistic({
          top:
            HEADER_HEIGHT +
            Math.max(
              0,
              Math.min(
                Math.round((currentTop - HEADER_HEIGHT - BAR_GAP) / ROW_HEIGHT),
                totalRows - 1,
              ),
            ) *
              ROW_HEIGHT +
            BAR_GAP,
        })
      }
    },
    [
      isDragging,
      startDate,
      dueDate,
      card,
      left,
      top,
      onBarClick,
      onBarMove,
      onBarReorder,
      onBarUnschedule,
      containerRef,
      currentTop,
      rowIndex,
      totalRows,
      dayWidth,
    ],
  )

  const handlePointerCancel = useCallback(() => {
    setIsDragging(false)
    setCurrentLeft(left)
    setCurrentTop(top)
    dragState.current = null
    setResizeMode(null)
    setResizeLeft(left)
    setResizeWidth(width)
    resizeState.current = null
    setOptimistic(null)
    containerRef?.current?.removeAttribute('data-bar-dragging')
  }, [left, top, width, containerRef])

  // Keep bar pinned to cursor when the container scrolls during drag (e.g. trackpad scroll)
  useEffect(() => {
    if (!isDragging) return
    const el = containerRef?.current
    if (!el) return
    const onScroll = () => {
      if (!dragState.current) return
      const scrollDelta = el.scrollLeft - dragState.current.initialScrollLeft
      const deltaX = lastPointerRef.current.x - dragState.current.startX
      const deltaY = lastPointerRef.current.y - dragState.current.startY
      setCurrentLeft(dragState.current.initialLeft + deltaX + scrollDelta)
      setCurrentTop(dragState.current.initialTop + deltaY)
    }
    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [isDragging, containerRef])

  const handleResizePointerDown = useCallback(
    (side: 'left' | 'right', e: React.PointerEvent<HTMLDivElement>) => {
      if (readOnly) return
      if (!startDate || !dueDate) return
      e.stopPropagation()
      e.currentTarget.setPointerCapture(e.pointerId)
      resizeState.current = {
        startX: e.clientX,
        initialLeft: left,
        initialWidth: Math.max(width, dayWidth),
      }
      setResizeLeft(left)
      setResizeWidth(Math.max(width, dayWidth))
      setResizeMode(side)
    },
    [left, width, startDate, dueDate, dayWidth, readOnly],
  )

  const handleResizePointerMove = useCallback(
    (e: React.PointerEvent<HTMLDivElement>) => {
      if (!resizeMode || !resizeState.current) return
      const deltaX = e.clientX - resizeState.current.startX
      const minWidth = dayWidth

      if (resizeMode === 'right') {
        const newWidth = Math.max(resizeState.current.initialWidth + deltaX, minWidth)
        setResizeWidth(newWidth)
      } else {
        const maxDelta = resizeState.current.initialWidth - minWidth
        const clampedDelta = Math.min(deltaX, maxDelta)
        setResizeLeft(resizeState.current.initialLeft + clampedDelta)
        setResizeWidth(resizeState.current.initialWidth - clampedDelta)
      }
    },
    [resizeMode, dayWidth],
  )

  const handleResizePointerUp = useCallback(
    (e: React.PointerEvent<HTMLDivElement>) => {
      // Snapshot resizeMode before nulling to avoid reading stale state in
      // concurrent mode where the setter could flush before subsequent reads.
      const currentResizeMode = resizeMode

      if (!currentResizeMode || !resizeState.current || !startDate || !dueDate) {
        setResizeMode(null)
        resizeState.current = null
        return
      }

      const deltaX = e.clientX - resizeState.current.startX
      setResizeMode(null)
      resizeState.current = null

      const dayDelta = Math.round(deltaX / dayWidth)

      if (dayDelta === 0) return

      const newStart = new Date(startDate)
      const newEnd = new Date(dueDate)

      if (currentResizeMode === 'right') {
        newEnd.setDate(newEnd.getDate() + dayDelta)
      } else {
        newStart.setDate(newStart.getDate() + dayDelta)
      }

      if (newStart > newEnd) return

      const snappedDelta = dayDelta * dayWidth
      if (currentResizeMode === 'right') {
        setOptimistic({ width: Math.max(width + snappedDelta, dayWidth) })
      } else {
        setOptimistic({
          left: left + snappedDelta,
          width: Math.max(width - snappedDelta, dayWidth),
        })
      }

      onBarResize?.(card.id, newStart.toISOString(), newEnd.toISOString())
    },
    [resizeMode, startDate, dueDate, card, left, width, onBarResize, dayWidth],
  )

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault()
        onBarClick?.(card)
      }
    },
    [card, onBarClick],
  )

  const displayLeft = isDragging
    ? currentLeft
    : resizeMode
      ? resizeLeft
      : (optimistic?.left ?? left)
  const displayTop = isDragging ? currentTop : (optimistic?.top ?? top)
  const displayWidth = resizeMode ? resizeWidth : Math.max(optimistic?.width ?? width, dayWidth)

  const estimatedMinutes = (card.estimatedHours ?? 0) * 60
  const spentMinutes = card.spentMinutes ?? 0
  const scheduledMinutes = Math.max(card.scheduledMinutes ?? 0, spentMinutes)
  const hasEstimate = estimatedMinutes > 0
  const spentRatio = hasEstimate ? Math.min(spentMinutes / estimatedMinutes, 1) : 0
  const scheduledRatio = hasEstimate ? Math.min(scheduledMinutes / estimatedMinutes, 1) : 0
  const isOverrun = hasEstimate && spentMinutes > estimatedMinutes
  const isEstimateFilled = hasEstimate && spentMinutes >= estimatedMinutes
  const hasHoursFill = hasEstimate && spentMinutes > 0
  const hasScheduledFill = hasEstimate && scheduledMinutes > spentMinutes
  const spentPercent = hasEstimate ? Math.round((spentMinutes / estimatedMinutes) * 100) : 0
  const scheduledPercent = hasEstimate ? Math.round((scheduledMinutes / estimatedMinutes) * 100) : 0
  const progressFillColor = theme.palette.primary.main
  const scheduledFillColor = alpha(progressFillColor, theme.palette.mode === 'dark' ? 0.28 : 0.22)

  // Estimated work is fully logged: suppress the red overdue treatment even if
  // the calendar end date has passed, since the planned hours are accounted for.
  const showAsOverdue = isOverdue && !isEstimateFilled

  // Flat styling: muted gray base, teal fill proportional to logged time.
  const mutedGray =
    theme.palette.mode === 'dark' ? theme.palette.grey[800] : theme.palette.grey[300]
  const baseColor = showAsOverdue ? theme.palette.error.main : mutedGray

  const solidColor = baseColor
  const bgColor = isFocused ? alpha(solidColor, 0.92) : solidColor
  const borderColor = alpha(theme.palette.divider, 1)
  const textColor = theme.palette.text.primary

  const completedSubtasks = card.subtasks?.filter((s) => s.completed).length ?? 0
  const totalSubtasks = card.subtasks?.length ?? 0

  const formatHours = (mins: number) => {
    if (mins <= 0) return '0h'
    const h = mins / 60
    if (h < 1) return `${Math.round(mins)}m`
    return h >= 10 ? `${Math.round(h)}h` : `${h.toFixed(1).replace(/\.0$/, '')}h`
  }

  const resizeHandleSx = {
    position: 'absolute' as const,
    top: 0,
    width: 5,
    height: '100%',
    cursor: 'col-resize',
    zIndex: 3,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    opacity: 0,
    transition: 'opacity 150ms ease',
    '&::after': {
      content: '""',
      width: 2,
      height: 16,
      borderRadius: 1,
      bgcolor: textColor,
      opacity: 0.7,
      boxShadow: `0 0 0 1px ${alpha(theme.palette.common.black, 0.15)}`,
    },
    '&:hover::after': {
      opacity: 1,
    },
  }

  const barHoverShowHandles = {
    '&:hover [data-testid^="resize-handle"]': {
      opacity: 1,
    },
  }

  const ariaLabel = `${card.title}${startDate && dueDate ? `, ${formatDateShort(startDate)} to ${formatDateShort(dueDate)}` : ''}${isOverdue ? ', overdue' : ''}${hasEstimate ? `, ${spentPercent}% of ${formatHours(estimatedMinutes)} logged${hasScheduledFill ? `, ${scheduledPercent}% scheduled` : ''}${isOverrun ? ' (overrun)' : ''}` : ''}`

  return (
    <Box
      role="button"
      tabIndex={0}
      aria-label={ariaLabel}
      onPointerDown={readOnly ? undefined : handlePointerDown}
      onPointerMove={readOnly ? undefined : handlePointerMove}
      onPointerUp={readOnly ? undefined : handlePointerUp}
      onPointerCancel={readOnly ? undefined : handlePointerCancel}
      onClick={readOnly ? () => onBarClick?.(card) : undefined}
      onKeyDown={handleKeyDown}
      sx={{
        position: 'absolute',
        left: displayLeft,
        top: displayTop,
        width: displayWidth,
        height: BAR_HEIGHT,
        bgcolor: bgColor,
        borderRadius: '8px',
        border: `1px solid ${borderColor}`,
        display: 'flex',
        alignItems: 'center',
        pl: '10px',
        pr: '8px',
        cursor: readOnly ? 'pointer' : isDragging ? 'grabbing' : 'grab',
        overflow: 'hidden',
        zIndex: isInteracting ? 10 : 2,
        userSelect: 'none',
        touchAction: 'none',
        transition: isInteracting ? 'none' : 'background-color 120ms ease, border-color 120ms ease',
        boxShadow: 'none',
        '&:hover': {
          bgcolor: isInteracting ? undefined : alpha(solidColor, 0.85),
          borderColor: isInteracting ? undefined : alpha(theme.palette.text.primary, 0.25),
        },
        ...barHoverShowHandles,
        '&:focus-visible': {
          outline: `2px solid ${theme.palette.primary.main}`,
          outlineOffset: 2,
        },
      }}
    >
      {/* Scheduled fill: lighter tint proportional to scheduled/estimated ratio */}
      {hasScheduledFill && (
        <Box
          aria-hidden
          sx={{
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: `${scheduledRatio * 100}%`,
            bgcolor: scheduledFillColor,
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
      )}

      {/* Clocked-hours fill: solid teal proportional to spent/estimated ratio */}
      {hasHoursFill && (
        <Box
          aria-hidden
          sx={{
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: `${spentRatio * 100}%`,
            bgcolor: progressFillColor,
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
      )}

      {showAsOverdue && (
        <Box
          sx={{
            position: 'absolute',
            top: -4,
            right: -4,
            width: 10,
            height: 10,
            borderRadius: '50%',
            bgcolor: 'error.main',
            border: '2px solid',
            borderColor: 'background.paper',
            zIndex: 4,
            boxShadow: `0 0 0 2px ${alpha(theme.palette.error.main, 0.3)}`,
            '@keyframes ganttOverduePulse': {
              '0%, 100%': { boxShadow: `0 0 0 2px ${alpha(theme.palette.error.main, 0.3)}` },
              '50%': { boxShadow: `0 0 0 5px ${alpha(theme.palette.error.main, 0)}` },
            },
            animation: 'ganttOverduePulse 2s ease-in-out infinite',
            '@media (prefers-reduced-motion: reduce)': {
              animation: 'none',
            },
          }}
        />
      )}

      {!readOnly && (
        <>
          <Box
            data-testid="resize-handle-left"
            onPointerDown={(e) => handleResizePointerDown('left', e)}
            onPointerMove={handleResizePointerMove}
            onPointerUp={handleResizePointerUp}
            onPointerCancel={handlePointerCancel}
            sx={{ ...resizeHandleSx, left: 0, borderRadius: '8px 0 0 8px' }}
          />

          <Box
            data-testid="resize-handle-right"
            onPointerDown={(e) => handleResizePointerDown('right', e)}
            onPointerMove={handleResizePointerMove}
            onPointerUp={handleResizePointerUp}
            onPointerCancel={handlePointerCancel}
            sx={{ ...resizeHandleSx, right: 0, borderRadius: '0 8px 8px 0' }}
          />
        </>
      )}

      <Typography
        variant="caption"
        sx={{
          position: 'relative',
          zIndex: 2,
          fontWeight: 600,
          color: textColor,
          fontSize: '0.6875rem',
          letterSpacing: '0.01em',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          flex: 1,
        }}
      >
        {card.title}
      </Typography>

      {totalSubtasks > 0 && displayWidth > 80 && (
        <Box
          sx={{
            position: 'relative',
            zIndex: 2,
            ml: 0.75,
            px: 0.75,
            py: 0.1,
            borderRadius: '10px',
            bgcolor: alpha(theme.palette.common.black, 0.22),
            display: 'flex',
            alignItems: 'center',
            flexShrink: 0,
          }}
        >
          <Typography
            variant="caption"
            sx={{
              color: textColor,
              fontSize: '0.625rem',
              fontWeight: 600,
              lineHeight: 1.4,
              letterSpacing: '0.02em',
            }}
          >
            {completedSubtasks}/{totalSubtasks}
          </Typography>
        </Box>
      )}
    </Box>
  )
})
