import Box from '@mui/material/Box'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { RefObject } from 'react'

interface BoardScrollbarProps {
  /** The horizontally scrolling element this slider controls. */
  scrollRef: RefObject<HTMLDivElement | null>
}

interface ScrollMetrics {
  scrollLeft: number
  scrollWidth: number
  clientWidth: number
  trackWidth: number
}

const MIN_THUMB_WIDTH_PX = 48
const SCROLLABLE_EPSILON_PX = 2

const INITIAL_METRICS: ScrollMetrics = {
  scrollLeft: 0,
  scrollWidth: 0,
  clientWidth: 0,
  trackWidth: 0,
}

/**
 * A draggable horizontal slider that mirrors and controls the board's
 * column rail. It stays in sync with the rail via scroll/resize/mutation
 * observers and hides itself when the rail isn't horizontally scrollable.
 *
 * Desktop-only: mobile uses scroll-snap plus the pager dots instead.
 */
export function BoardScrollbar({ scrollRef }: BoardScrollbarProps) {
  const trackRef = useRef<HTMLDivElement | null>(null)
  const dragStartRef = useRef<{ pointerX: number; scrollLeft: number } | null>(null)
  const [metrics, setMetrics] = useState<ScrollMetrics>(INITIAL_METRICS)

  const measure = useCallback(() => {
    const scroller = scrollRef.current
    if (!scroller) return
    setMetrics({
      scrollLeft: scroller.scrollLeft,
      scrollWidth: scroller.scrollWidth,
      clientWidth: scroller.clientWidth,
      trackWidth: trackRef.current?.clientWidth ?? scroller.clientWidth,
    })
  }, [scrollRef])

  useEffect(() => {
    const scroller = scrollRef.current
    if (!scroller) return

    measure()
    scroller.addEventListener('scroll', measure, { passive: true })

    const resizeObserver = new ResizeObserver(measure)
    resizeObserver.observe(scroller)
    if (trackRef.current) {
      resizeObserver.observe(trackRef.current)
    }

    // Columns added, removed, or resized change the scrollable width.
    const mutationObserver = new MutationObserver(measure)
    mutationObserver.observe(scroller, { childList: true, subtree: true })

    window.addEventListener('resize', measure)

    return () => {
      scroller.removeEventListener('scroll', measure)
      resizeObserver.disconnect()
      mutationObserver.disconnect()
      window.removeEventListener('resize', measure)
    }
  }, [measure, scrollRef])

  const { scrollLeft, scrollWidth, clientWidth, trackWidth } = metrics
  const maxScrollLeft = scrollWidth - clientWidth
  const isScrollable = maxScrollLeft > SCROLLABLE_EPSILON_PX

  const thumbWidth = Math.max(
    MIN_THUMB_WIDTH_PX,
    scrollWidth > 0 ? (clientWidth / scrollWidth) * trackWidth : trackWidth,
  )
  const maxThumbOffset = Math.max(0, trackWidth - thumbWidth)
  const thumbOffset = maxScrollLeft > 0 ? (scrollLeft / maxScrollLeft) * maxThumbOffset : 0

  const handleThumbMouseDown = useCallback(
    (event: React.MouseEvent) => {
      event.preventDefault()
      event.stopPropagation()
      const scroller = scrollRef.current
      if (!scroller) return

      dragStartRef.current = { pointerX: event.clientX, scrollLeft: scroller.scrollLeft }

      const onMouseMove = (moveEvent: MouseEvent) => {
        const dragStart = dragStartRef.current
        const track = trackRef.current
        const node = scrollRef.current
        if (!dragStart || !track || !node) return

        const currentTrackWidth = track.clientWidth
        const currentThumbWidth = Math.max(
          MIN_THUMB_WIDTH_PX,
          node.scrollWidth > 0
            ? (node.clientWidth / node.scrollWidth) * currentTrackWidth
            : currentTrackWidth,
        )
        const currentMaxOffset = currentTrackWidth - currentThumbWidth
        const currentMaxScroll = node.scrollWidth - node.clientWidth
        if (currentMaxOffset <= 0 || currentMaxScroll <= 0) return

        const deltaX = moveEvent.clientX - dragStart.pointerX
        const scrollDelta = (deltaX / currentMaxOffset) * currentMaxScroll
        node.scrollLeft = dragStart.scrollLeft + scrollDelta
      }

      const onMouseUp = () => {
        dragStartRef.current = null
        document.removeEventListener('mousemove', onMouseMove)
        document.removeEventListener('mouseup', onMouseUp)
        document.body.style.userSelect = ''
        document.body.style.cursor = ''
      }

      document.body.style.userSelect = 'none'
      document.body.style.cursor = 'grabbing'
      document.addEventListener('mousemove', onMouseMove)
      document.addEventListener('mouseup', onMouseUp)
    },
    [scrollRef],
  )

  const handleTrackMouseDown = useCallback(
    (event: React.MouseEvent) => {
      // Ignore clicks that originate on the thumb; those start a drag.
      if (event.target !== trackRef.current) return
      const scroller = scrollRef.current
      const track = trackRef.current
      if (!scroller || !track) return

      const rect = track.getBoundingClientRect()
      const clickX = event.clientX - rect.left
      const currentThumbWidth = Math.max(
        MIN_THUMB_WIDTH_PX,
        scroller.scrollWidth > 0
          ? (scroller.clientWidth / scroller.scrollWidth) * track.clientWidth
          : track.clientWidth,
      )
      const currentMaxOffset = track.clientWidth - currentThumbWidth
      if (currentMaxOffset <= 0) return

      const ratio = Math.max(0, Math.min(1, (clickX - currentThumbWidth / 2) / currentMaxOffset))
      scroller.scrollTo({
        left: ratio * (scroller.scrollWidth - scroller.clientWidth),
        behavior: 'smooth',
      })
    },
    [scrollRef],
  )

  return (
    <Box
      ref={trackRef}
      onMouseDown={handleTrackMouseDown}
      sx={{
        display: { xs: 'none', sm: isScrollable ? 'block' : 'none' },
        position: 'fixed',
        bottom: 16,
        left: 128,
        right: 64,
        zIndex: 5,
        width: 'auto',
        height: 12,
        mt: 0,
        mb: 1,
        borderRadius: 999,
        bgcolor: 'action.hover',
        cursor: 'pointer',
        userSelect: 'none',
        opacity: 0,
        transition: 'opacity 150ms ease',
        '&:hover, &:focus-within': {
          opacity: 1,
        },
      }}
    >
      <Box
        onMouseDown={handleThumbMouseDown}
        sx={{
          position: 'absolute',
          top: 2,
          bottom: 2,
          left: 0,
          width: `${thumbWidth}px`,
          transform: `translateX(${thumbOffset}px)`,
          borderRadius: 999,
          bgcolor: 'action.active',
          opacity: 0.45,
          cursor: 'grab',
          transition: 'opacity 150ms ease, background-color 150ms ease',
          '&:hover': {
            opacity: 0.7,
          },
          '&:active': {
            opacity: 0.85,
            cursor: 'grabbing',
          },
        }}
      />
    </Box>
  )
}
