import { useCallback, useRef, useState } from 'react'
import { useDraggable } from '@dnd-kit/core'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/Close'
import DragHandleIcon from '@mui/icons-material/DragHandle'
import { timeToY, blockHeight, yToTime, ROW_HEIGHT_PX } from '@/features/planner/utils/plannerUtils'
import type { PlannedBlock } from '@/lib/types'

type PlannerTimeBlockProps = {
  block: PlannedBlock
  onDelete?: (blockId: string) => void
  onResize?: (blockId: string, newEndTime: string) => void
  onClick?: (cardId: string) => void
  isDragOverlay?: boolean
  overlapColumn?: number
  overlapTotalColumns?: number
}

const MIN_BLOCK_HEIGHT = 24
const RESIZE_HANDLE_HEIGHT = 10

export function PlannerTimeBlock({
  block,
  onDelete,
  onResize,
  onClick,
  isDragOverlay,
  overlapColumn = 0,
  overlapTotalColumns = 1,
}: PlannerTimeBlockProps) {
  const top = timeToY(block.startTime)
  const height = blockHeight(block.startTime, block.endTime)
  const actualHeight = Math.max(height, MIN_BLOCK_HEIGHT)

  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `block-${block.id}`,
    data: { type: 'block', block },
    disabled: isDragOverlay,
  })

  const [isResizing, setIsResizing] = useState(false)
  const [resizeHeight, setResizeHeight] = useState<number | null>(null)
  const resizeStartRef = useRef<{ startY: number; startHeight: number } | null>(null)
  const pointerDownPosRef = useRef<{ x: number; y: number } | null>(null)
  const justResizedRef = useRef(false)

  const handleResizeStart = useCallback(
    (e: React.PointerEvent) => {
      e.stopPropagation()
      e.preventDefault()
      const startY = e.clientY
      const startHeight = actualHeight

      resizeStartRef.current = { startY, startHeight }
      setIsResizing(true)
      setResizeHeight(startHeight)

      const target = e.currentTarget as HTMLElement
      target.setPointerCapture(e.pointerId)
    },
    [actualHeight],
  )

  const handleResizeMove = useCallback(
    (e: React.PointerEvent) => {
      if (!resizeStartRef.current) return
      const delta = e.clientY - resizeStartRef.current.startY
      const raw = resizeStartRef.current.startHeight + delta
      const snapped = Math.max(ROW_HEIGHT_PX, Math.round(raw / ROW_HEIGHT_PX) * ROW_HEIGHT_PX)
      setResizeHeight(snapped)
    },
    [],
  )

  const handleResizeEnd = useCallback(
    (e: React.PointerEvent) => {
      if (!resizeStartRef.current || resizeHeight === null) {
        setIsResizing(false)
        resizeStartRef.current = null
        return
      }

      const target = e.currentTarget as HTMLElement
      target.releasePointerCapture(e.pointerId)

      const newEndY = timeToY(block.startTime) + resizeHeight
      const newEndTime = yToTime(newEndY)

      if (newEndTime !== block.endTime && onResize) {
        onResize(block.id, newEndTime)
      }

      setIsResizing(false)
      setResizeHeight(null)
      resizeStartRef.current = null
      justResizedRef.current = true
      requestAnimationFrame(() => {
        justResizedRef.current = false
      })
    },
    [block.id, block.startTime, block.endTime, resizeHeight, onResize],
  )

  const handlePointerDown = useCallback((e: React.PointerEvent) => {
    pointerDownPosRef.current = { x: e.clientX, y: e.clientY }
  }, [])

  const handleClick = useCallback(
    (e: React.MouseEvent) => {
      if (!onClick || isResizing || justResizedRef.current) return
      // Distinguish click from drag: 5px threshold on pointer movement.
      if (pointerDownPosRef.current) {
        const dx = Math.abs(e.clientX - pointerDownPosRef.current.x)
        const dy = Math.abs(e.clientY - pointerDownPosRef.current.y)
        if (dx > 5 || dy > 5) return
      }
      onClick(block.cardId)
    },
    [onClick, block.cardId, isResizing],
  )

  const displayHeight = isResizing && resizeHeight !== null ? resizeHeight : actualHeight

  const columnWidthPercent = 100 / overlapTotalColumns
  const leftPercent = overlapColumn * columnWidthPercent
  const gapPx = overlapTotalColumns > 1 ? 2 : 0

  const timeRange = `${block.startTime.slice(0, 5)} – ${block.endTime.slice(0, 5)}`

  const blockContent = (
    <Box
      ref={setNodeRef}
      {...(isResizing ? {} : listeners)}
      {...attributes}
      onPointerDown={handlePointerDown}
      onClick={handleClick}
      sx={{
        position: isDragOverlay ? 'relative' : 'absolute',
        top: isDragOverlay ? 0 : top,
        left: isDragOverlay ? 0 : `calc(${leftPercent}% + ${gapPx}px)`,
        width: isDragOverlay ? 240 : `calc(${columnWidthPercent}% - ${gapPx * 2}px)`,
        height: displayHeight,
        bgcolor: 'primary.main',
        color: 'primary.contrastText',
        borderRadius: 1,
        px: 1,
        py: 0.5,
        overflow: 'hidden',
        cursor: isResizing ? 'ns-resize' : onClick ? 'pointer' : 'grab',
        display: 'flex',
        flexDirection: 'column',
        opacity: isDragging ? 0.5 : 1,
        userSelect: 'none',
        zIndex: 1,
        '&:hover .planner-block-delete': {
          opacity: 1,
        },
        '&:hover .planner-resize-handle': {
          opacity: 1,
        },
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0 }}>
        <Typography
          variant="caption"
          sx={{
            fontWeight: 600,
            fontSize: '0.75rem',
            lineHeight: 1.3,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            flex: 1,
            minWidth: 0,
            color: 'inherit',
          }}
        >
          {block.cardTitle}
        </Typography>
        {onDelete && (
          <IconButton
            className="planner-block-delete"
            size="small"
            onClick={(e) => {
              e.stopPropagation()
              onDelete(block.id)
            }}
            aria-label={`Remove ${block.cardTitle} from planner`}
            sx={{
              p: 0,
              opacity: 0,
              color: 'inherit',
              ml: 'auto',
              flexShrink: 0,
            }}
          >
            <CloseIcon sx={{ fontSize: 14 }} />
          </IconButton>
        )}
      </Box>
      {displayHeight > 36 && (
        <Typography
          variant="caption"
          sx={{ fontSize: '0.6875rem', opacity: 0.85, color: 'inherit' }}
        >
          {timeRange}
        </Typography>
      )}

      {/* Resize handle on bottom edge */}
      {!isDragOverlay && onResize && (
        <Box
          className="planner-resize-handle"
          onPointerDown={handleResizeStart}
          onPointerMove={handleResizeMove}
          onPointerUp={handleResizeEnd}
          sx={{
            position: 'absolute',
            bottom: 0,
            left: 0,
            right: 0,
            height: RESIZE_HANDLE_HEIGHT,
            cursor: 'ns-resize',
            opacity: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            bgcolor: 'rgba(255,255,255,0.2)',
            borderBottomLeftRadius: 4,
            borderBottomRightRadius: 4,
            '&:hover': {
              opacity: 1,
            },
          }}
        >
          <DragHandleIcon sx={{ fontSize: 12, color: 'inherit', opacity: 0.7 }} />
        </Box>
      )}
    </Box>
  )

  // Wrap with Tooltip for small blocks that don't show time range
  if (displayHeight <= 36 && !isDragOverlay) {
    return (
      <Tooltip title={`${block.cardTitle} · ${timeRange}`} placement="top" arrow>
        {blockContent}
      </Tooltip>
    )
  }

  return blockContent
}
