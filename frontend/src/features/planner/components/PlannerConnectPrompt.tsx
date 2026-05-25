import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import GoogleIcon from '@mui/icons-material/Google'
import { getGoogleAuthUrl } from '@/features/planner/api/google'

export function PlannerConnectPrompt() {
  const [loading, setLoading] = useState(false)

  const handleConnect = async () => {
    setLoading(true)
    try {
      const url = await getGoogleAuthUrl()
      window.location.href = url
    } finally {
      setLoading(false)
    }
  }

  return (
    <Alert
      severity="info"
      variant="outlined"
      icon={<GoogleIcon fontSize="small" />}
      action={
        <Button
          size="small"
          onClick={handleConnect}
          disabled={loading}
        >
          {loading ? 'Connecting…' : 'Connect'}
        </Button>
      }
      sx={{ mb: 2 }}
    >
      Connect your Google account to see calendar events alongside your planned blocks.
    </Alert>
  )
}
