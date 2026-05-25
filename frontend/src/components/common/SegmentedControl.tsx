import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'

export interface SegmentedControlOption {
  label: string
  count?: number
}

interface SegmentedControlProps {
  value: number
  onChange: (index: number) => void
  options: SegmentedControlOption[]
}

export function SegmentedControl({ value, onChange, options }: SegmentedControlProps) {
  return (
    <Box
      role="tablist"
      sx={{
        display: 'inline-flex',
        alignSelf: { xs: 'stretch', sm: 'flex-start' },
        maxWidth: '100%',
        bgcolor: (theme) =>
          theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.04)',
        borderRadius: 1,
        p: 0.5,
        gap: 0.5,
      }}
    >
      {options.map((opt, i) => (
        <Box
          key={i}
          onClick={() => onChange(i)}
          role="tab"
          aria-selected={value === i}
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              onChange(i)
            }
          }}
          sx={{
            px: { xs: 1.25, sm: 2 },
            py: 0.75,
            borderRadius: 0.75,
            cursor: 'pointer',
            fontWeight: 600,
            fontSize: { xs: '0.8125rem', sm: '0.875rem' },
            lineHeight: 1.5,
            userSelect: 'none',
            display: 'flex',
            alignItems: 'center',
            justifyContent: { xs: 'center', sm: 'flex-start' },
            flex: { xs: 1, sm: 'initial' },
            minWidth: 0,
            gap: { xs: 0.5, sm: 1 },
            whiteSpace: 'nowrap',
            transition: 'background-color 100ms ease, color 100ms ease',
            bgcolor: value === i ? 'background.paper' : 'transparent',
            color: value === i ? 'text.primary' : 'text.secondary',
            boxShadow: value === i ? 1 : 'none',
            '&:hover': {
              color: 'text.primary',
            },
          }}
        >
          {opt.label}
          {opt.count !== undefined ? (
            <Chip
              label={opt.count}
              size="small"
              color={value === i ? 'primary' : 'default'}
              sx={{
                height: { xs: 18, sm: 20 },
                fontSize: { xs: '0.6875rem', sm: '0.75rem' },
                fontWeight: 600,
                minWidth: { xs: 20, sm: 24 },
                '& .MuiChip-label': { px: { xs: 0.75, sm: 1 } },
              }}
            />
          ) : null}
        </Box>
      ))}
    </Box>
  )
}
