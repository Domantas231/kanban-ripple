import { Box, CircularProgress, Typography } from '@mui/material'

type FullPageSpinnerProps = {
  label?: string
}

export function FullPageSpinner({ label = 'Loading workspace...' }: FullPageSpinnerProps) {
  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{
        minHeight: '100vh',
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        px: 3,
      }}
    >
      <Box
        sx={{
          width: 'min(420px, 100%)',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 2,
          py: 5,
        }}
      >
        <CircularProgress size={40} thickness={4} aria-label={label} />
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
      </Box>
    </Box>
  )
}
