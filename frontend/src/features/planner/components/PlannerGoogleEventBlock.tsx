import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { timeToY, PLANNER_START_HOUR, PLANNER_END_HOUR, SLOT_HEIGHT_PX } from '@/features/planner/utils/plannerUtils'
import type { GoogleCalendarEvent } from '@/lib/types'

type PlannerGoogleEventBlockProps = {
  event: GoogleCalendarEvent
}

function isoToTimeString(iso: string): string {
  const d = new Date(iso)
  const h = d.getHours()
  const m = d.getMinutes()
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`
}

function clampTime(time: string): string {
  const [h, m] = time.split(':').map(Number)
  if (h < PLANNER_START_HOUR) return `${PLANNER_START_HOUR.toString().padStart(2, '0')}:00`
  if (h >= PLANNER_END_HOUR) return `${PLANNER_END_HOUR.toString().padStart(2, '0')}:00`
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`
}

const MIN_HEIGHT = 20

export function PlannerGoogleEventBlock({ event }: PlannerGoogleEventBlockProps) {
  const startTime = clampTime(isoToTimeString(event.start))
  const endTime = clampTime(isoToTimeString(event.end))

  const top = timeToY(startTime)
  const bottom = timeToY(endTime)
  const height = Math.max(bottom - top, MIN_HEIGHT)

  if (top >= (PLANNER_END_HOUR - PLANNER_START_HOUR) * SLOT_HEIGHT_PX) return null
  if (bottom <= 0) return null

  const displayStart = isoToTimeString(event.start)
  const displayEnd = isoToTimeString(event.end)

  const handleClick = () => {
    if (event.htmlLink) {
      window.open(event.htmlLink, '_blank', 'noopener,noreferrer')
    }
  }

  return (
    <Box
      onClick={handleClick}
      sx={{
        position: 'absolute',
        top,
        left: 4,
        right: 4,
        height,
        bgcolor: 'rgba(66, 133, 244, 0.12)',
        border: 1,
        borderColor: 'rgba(66, 133, 244, 0.3)',
        borderRadius: 1,
        px: 1,
        py: 0.5,
        overflow: 'hidden',
        pointerEvents: 'auto',
        zIndex: 0,
        display: 'flex',
        flexDirection: 'column',
        cursor: event.htmlLink ? 'pointer' : 'default',
        '&:hover': event.htmlLink ? {
          bgcolor: 'rgba(66, 133, 244, 0.2)',
          borderColor: 'rgba(66, 133, 244, 0.5)',
        } : {},
      }}
    >
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, minWidth: 0 }}>
        <Typography
          variant="caption"
          sx={{
            fontWeight: 500,
            fontSize: '0.7rem',
            lineHeight: 1.3,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
            flex: 1,
            minWidth: 0,
            color: 'rgb(66, 133, 244)',
          }}
        >
          {event.summary}
        </Typography>
        {event.htmlLink && (
          <OpenInNewIcon sx={{ fontSize: 12, color: 'rgb(66, 133, 244)', opacity: 0.6, flexShrink: 0 }} />
        )}
      </Box>
      {height > 30 && (
        <Typography
          variant="caption"
          sx={{ fontSize: '0.625rem', color: 'rgb(66, 133, 244)', opacity: 0.7 }}
        >
          {displayStart} &ndash; {displayEnd}
        </Typography>
      )}
    </Box>
  )
}
