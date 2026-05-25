import { useQuery } from '@tanstack/react-query'
import Box from '@mui/material/Box'
import { plannerQueryKeys } from '@/features/planner/api/query-keys'
import { getGoogleCalendarEvents } from '@/features/planner/api/planner'
import { PlannerGoogleEventBlock } from './PlannerGoogleEventBlock'

type PlannerGoogleEventsProps = {
  date: string
  isConnected: boolean
}

export function PlannerGoogleEvents({ date, isConnected }: PlannerGoogleEventsProps) {
  const { data: events } = useQuery({
    queryKey: plannerQueryKeys.googleCalendarEvents(date),
    queryFn: () => getGoogleCalendarEvents(date),
    enabled: isConnected,
  })

  if (!isConnected || !events?.length) return null

  return (
    <Box
      sx={{
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        pointerEvents: 'none',
        zIndex: 0,
        '& > *': {
          pointerEvents: 'auto',
        },
      }}
    >
      {events.map((event) => (
        <PlannerGoogleEventBlock key={event.id} event={event} />
      ))}
    </Box>
  )
}
