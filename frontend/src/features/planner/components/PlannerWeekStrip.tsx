import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { format, addDays, startOfWeek, isSameDay, isToday } from 'date-fns'
import { plannerQueryKeys } from '@/features/planner/api/query-keys'
import { getPlannedBlocks } from '@/features/planner/api/planner'
import { formatDateParam } from '@/features/planner/utils/plannerUtils'
import type { Guid } from '@/lib/types'

type PlannerWeekStripProps = {
  projectId: Guid
  selectedDate: Date
  onDateChange: (date: Date) => void
}

export function PlannerWeekStrip({ projectId, selectedDate, onDateChange }: PlannerWeekStripProps) {
  const weekStart = useMemo(() => startOfWeek(selectedDate, { weekStartsOn: 1 }), [selectedDate])
  const weekDays = useMemo(
    () => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)),
    [weekStart],
  )

  return (
    <Box sx={{ display: 'flex', gap: 0.5 }}>
      {weekDays.map((day) => (
        <WeekDay
          key={day.toISOString()}
          day={day}
          projectId={projectId}
          isSelected={isSameDay(day, selectedDate)}
          isCurrentDay={isToday(day)}
          onClick={() => onDateChange(day)}
        />
      ))}
    </Box>
  )
}

type WeekDayProps = {
  day: Date
  projectId: Guid
  isSelected: boolean
  isCurrentDay: boolean
  onClick: () => void
}

function WeekDay({ day, projectId, isSelected, isCurrentDay, onClick }: WeekDayProps) {
  const dateParam = formatDateParam(day)

  const { data: blocks } = useQuery({
    queryKey: plannerQueryKeys.plannerBlocks(projectId, dateParam),
    queryFn: () => getPlannedBlocks(projectId, dateParam),
    staleTime: 60_000,
  })

  const blockCount = blocks?.length ?? 0
  const maxDots = Math.min(blockCount, 4)

  return (
    <Box
      onClick={onClick}
      sx={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        py: 0.75,
        px: 0.5,
        borderRadius: 1,
        cursor: 'pointer',
        bgcolor: isSelected ? 'action.selected' : 'transparent',
        border: 1,
        borderColor: isSelected ? 'primary.main' : 'transparent',
        '&:hover': {
          bgcolor: isSelected ? 'action.selected' : 'action.hover',
        },
      }}
    >
      <Typography
        variant="caption"
        sx={{
          fontSize: '0.625rem',
          fontWeight: 500,
          color: isCurrentDay ? 'primary.main' : 'text.secondary',
          lineHeight: 1,
          textTransform: 'uppercase',
        }}
      >
        {format(day, 'EEE')}
      </Typography>
      <Typography
        variant="body2"
        sx={{
          fontWeight: isSelected || isCurrentDay ? 700 : 500,
          fontSize: '0.8125rem',
          lineHeight: 1.4,
          color: isCurrentDay ? 'primary.main' : 'text.primary',
        }}
      >
        {format(day, 'd')}
      </Typography>
      {/* Block density dots */}
      <Box sx={{ display: 'flex', gap: '2px', mt: '2px', minHeight: 6 }}>
        {Array.from({ length: maxDots }, (_, i) => (
          <Box
            key={i}
            sx={{
              width: 4,
              height: 4,
              borderRadius: '50%',
              bgcolor: isSelected ? 'primary.main' : 'text.disabled',
            }}
          />
        ))}
      </Box>
    </Box>
  )
}
