import Box from '@mui/material/Box'
import CircularProgress from '@mui/material/CircularProgress'
import Typography from '@mui/material/Typography'
import WifiOffOutlinedIcon from '@mui/icons-material/WifiOffOutlined'
import { useRealtimeStore } from '../stores/realtimeStore'

export function ConnectionStatusBanner() {
  const connectionState = useRealtimeStore((state) => state.connectionState)

  if (connectionState === 'connected' || connectionState === 'disconnected') {
    return null
  }

  const isReconnecting = connectionState === 'reconnecting'

  return (
    <Box
      role="status"
      aria-live="polite"
      sx={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        zIndex: 50,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1,
        py: 0.75,
        px: 2,
        bgcolor: (theme) =>
          theme.palette.mode === 'dark'
            ? 'rgba(245, 158, 11, 0.15)'
            : 'rgba(217, 119, 6, 0.1)',
        borderBottom: '1px solid',
        borderColor: 'warning.main',
      }}
    >
      {isReconnecting ? (
        <CircularProgress size={14} thickness={5} color="warning" />
      ) : (
        <WifiOffOutlinedIcon sx={{ fontSize: 16, color: 'warning.main' }} />
      )}
      <Typography variant="caption" fontWeight={500} color="warning.main">
        {isReconnecting ? 'Reconnecting...' : 'Connecting...'}
      </Typography>
    </Box>
  )
}
