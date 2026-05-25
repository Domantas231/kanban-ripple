import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useAuthStore } from '@/features/auth'
import { acceptInvitation } from '@/features/invitations/api/invitations'

type AcceptStatus = 'idle' | 'success' | 'error'

interface InvitationAcceptPageProps {
  token?: string
}

export function InvitationAcceptPage({ token }: InvitationAcceptPageProps) {
  const navigate = useNavigate()
  const hasAttempted = useRef(false)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)

  const [status, setStatus] = useState<AcceptStatus>('idle')
  const [errorMessage, setErrorMessage] = useState<string>('')

  const redirectTo = useMemo(() => {
    if (!token) {
      return '/invitations/accept'
    }
    return `/invitations/accept?token=${encodeURIComponent(token)}`
  }, [token])

  useEffect(() => {
    if (!isAuthenticated || !token || hasAttempted.current) {
      return
    }

    hasAttempted.current = true

    void (async () => {
      try {
        await acceptInvitation(token)
        setStatus('success')
      } catch (error) {
        const message =
          typeof error === 'object' &&
          error !== null &&
          'response' in error &&
          typeof (error as { response?: { data?: { message?: string } } }).response?.data?.message === 'string'
            ? (error as { response?: { data?: { message?: string } } }).response?.data?.message
            : 'Unable to accept invitation.'

        setErrorMessage(message ?? 'Unable to accept invitation.')
        setStatus('error')
      }
    })()
  }, [isAuthenticated, token])

  return (
    <Box sx={{ px: 3, py: 3, display: 'flex', justifyContent: 'center' }}>
      <Card variant="outlined" sx={{ width: '100%', maxWidth: 560 }}>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h5">Workspace Invitation</Typography>

            {!token ? <Alert severity="error">Missing invitation token.</Alert> : null}

            {!isAuthenticated ? (
              <Alert severity="info">
                Please sign in with the invited account email, then continue to accept this invitation.
              </Alert>
            ) : null}

            {isAuthenticated && token && status === 'idle' ? (
              <Typography color="text.secondary">Accepting invitation...</Typography>
            ) : null}

            {status === 'success' ? (
              <Alert severity="success">Invitation accepted. You can now access the workspace.</Alert>
            ) : null}

            {status === 'error' ? <Alert severity="error">{errorMessage}</Alert> : null}

            <Stack direction="row" spacing={1.5}>
              {!isAuthenticated ? (
                <Button
                  variant="contained"
                  onClick={() => navigate({ to: '/login', search: { redirect: redirectTo } })}
                >
                  Go to Login
                </Button>
              ) : null}

              <Button variant="outlined" onClick={() => navigate({ to: '/projects' })}>
                Go to Workspaces
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  )
}
