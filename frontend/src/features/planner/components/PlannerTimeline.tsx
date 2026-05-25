import { useEffect, useMemo, useRef, useState } from 'react'
import { useDroppable } from '@dnd-kit/core'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import {
  HOUR_LABELS,
  SLOT_HEIGHT_PX,
  TIMELINE_HEIGHT_PX,
  TOTAL_HOURS,
  currentTimeY,
  computeOverlapLayout,
} from '@/features/planner/utils/plannerUtils'
import { PlannerTimeBlock } from './PlannerTimeBlock'
import { PlannerGoogleEvents } from './PlannerGoogleEvents'
import type { PlannedBlock } from '@/lib/types'

export const GUTTER_WIDTH = 56

type DropPreview = {
  top: number
  height: number
}

type PlannerTimelineProps = {
  blocks: PlannedBlock[]
  onDeleteBlock?: (blockId: string) => void
  onResizeBlock?: (blockId: string, newEndTime: string) => void
  onBlockClick?: (cardId: string) => void
  googleDate?: string
  googleConnected?: boolean
  dropPreview?: DropPreview | null
  snapTime?: string | null
  autoScrollRef?: React.RefObject<HTMLDivElement | null>
  isToday?: boolean
}

export function PlannerTimeline({
  blocks,
  onDeleteBlock,
  onResizeBlock,
  onBlockClick,
  googleDate,
  googleConnected,
  dropPreview,
  snapTime,
  autoScrollRef,
  isToday = false,
}: PlannerTimelineProps) {
  const { setNodeRef, isOver } = useDroppable({
    id: 'timeline-drop-area',
    data: { type: 'timeline' },
  })

  const [nowY, setNowY] = useState<number | null>(isToday ? currentTimeY() : null)
  const [prevIsToday, setPrevIsToday] = useState(isToday)
  const nowLineRef = useRef<HTMLDivElement>(null)

  if (isToday !== prevIsToday) {
    setPrevIsToday(isToday)
    setNowY(isToday ? currentTimeY() : null)
  }

  // Update current time line every 30 seconds (only when viewing today)
  useEffect(() => {
    if (!isToday) return
    const interval = setInterval(() => setNowY(currentTimeY()), 30_000)
    return () => clearInterval(interval)
  }, [isToday])

  // Auto-scroll to current time on mount
  useEffect(() => {
    if (nowY !== null && autoScrollRef?.current) {
      const scrollContainer = autoScrollRef.current
      const targetScroll = Math.max(0, nowY - 120)
      scrollContainer.scrollTop = targetScroll
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const overlapLayout = useMemo(() => computeOverlapLayout(blocks), [blocks])

  return (
    <Box
      sx={{
        position: 'relative',
        height: TIMELINE_HEIGHT_PX,
        flex: 1,
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
                pr: 1.5,
                fontSize: '0.75rem',
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

      {/* 15-min sub-grid lines (lighter) */}
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
          ref={nowLineRef}
          sx={{
            position: 'absolute',
            top: nowY,
            left: GUTTER_WIDTH - 6,
            right: 0,
            zIndex: 3,
            pointerEvents: 'none',
          }}
        >
          <Box
            sx={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              bgcolor: 'error.main',
              position: 'absolute',
              top: -3.5,
              left: 0,
            }}
          />
          <Box
            sx={{
              position: 'absolute',
              top: 0,
              left: 8,
              right: 0,
              height: '2px',
              bgcolor: 'error.main',
            }}
          />
        </Box>
      )}

      {/* Droppable blocks area */}
      <Box
        ref={setNodeRef}
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
        {/* Drop preview */}
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

        {/* Snap time tooltip */}
        {snapTime && dropPreview && (
          <Box
            sx={{
              position: 'absolute',
              top: dropPreview.top - 24,
              left: 4,
              bgcolor: 'grey.800',
              color: 'common.white',
              px: 0.75,
              py: 0.25,
              borderRadius: 0.5,
              fontSize: '0.6875rem',
              fontWeight: 600,
              zIndex: 10,
              pointerEvents: 'none',
            }}
          >
            {snapTime}
          </Box>
        )}

        {googleDate && (
          <PlannerGoogleEvents date={googleDate} isConnected={googleConnected ?? false} />
        )}

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
            <DragIndicatorIcon sx={{ fontSize: 32, color: 'text.disabled', mb: 0.5 }} />
            <Typography variant="body2" color="text.disabled">
              Drag cards here to plan your day
            </Typography>
          </Box>
        )}

        {blocks.map((block) => {
          const layout = overlapLayout.get(block.id)
          return (
            <PlannerTimeBlock
              key={block.id}
              block={block}
              onDelete={onDeleteBlock}
              onResize={onResizeBlock}
              onClick={onBlockClick}
              overlapColumn={layout?.column}
              overlapTotalColumns={layout?.totalColumns}
            />
          )
        })}
      </Box>
    </Box>
  )
}
