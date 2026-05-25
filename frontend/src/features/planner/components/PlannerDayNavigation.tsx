import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import TodayIcon from '@mui/icons-material/Today'
import { DatePicker } from '@mui/x-date-pickers/DatePicker'
import { format, addDays, subDays, isToday } from 'date-fns'

type PlannerDayNavigationProps = {
  selectedDate: Date
  onDateChange: (date: Date) => void
  compact?: boolean
}

export function PlannerDayNavigation({ selectedDate, onDateChange, compact }: PlannerDayNavigationProps) {
  const handlePrev = () => onDateChange(subDays(selectedDate, 1))
  const handleNext = () => onDateChange(addDays(selectedDate, 1))
  const handleToday = () => onDateChange(new Date())

  const dayLabel = isToday(selectedDate)
    ? 'Today'
    : format(selectedDate, 'EEEE')

  if (compact) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <IconButton onClick={handlePrev} size="small" aria-label="Previous day">
          <ChevronLeftIcon sx={{ fontSize: 18 }} />
        </IconButton>
        <IconButton onClick={handleNext} size="small" aria-label="Next day">
          <ChevronRightIcon sx={{ fontSize: 18 }} />
        </IconButton>
        <Box sx={{ ml: 'auto', display: 'flex', alignItems: 'center', gap: 0.5 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, fontSize: '0.8125rem' }}>
            {dayLabel}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {format(selectedDate, 'MMM d')}
          </Typography>
        </Box>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <IconButton onClick={handlePrev} size="small" aria-label="Previous day">
        <ChevronLeftIcon fontSize="small" />
      </IconButton>
      <IconButton onClick={handleNext} size="small" aria-label="Next day">
        <ChevronRightIcon fontSize="small" />
      </IconButton>
      {!isToday(selectedDate) && (
        <Button
          size="small"
          variant="outlined"
          startIcon={<TodayIcon />}
          onClick={handleToday}
          sx={{ textTransform: 'none', minWidth: 'auto' }}
        >
          Today
        </Button>
      )}
      <Typography variant="h3" sx={{ fontWeight: 600, ml: 1 }}>
        {dayLabel}
      </Typography>
      <Box sx={{ ml: 'auto', display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography variant="body1" color="text.secondary">
          {format(selectedDate, 'MMM d, yyyy')}
        </Typography>
        <DatePicker
          value={selectedDate}
          onChange={(val) => val && onDateChange(val)}
          slotProps={{
            textField: {
              size: 'small',
              sx: { width: 160 },
            },
          }}
        />
      </Box>
    </Box>
  )
}
