import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import GoogleIcon from '@mui/icons-material/Google'
import LinkOffIcon from '@mui/icons-material/LinkOff'
import { disconnectGoogle, getGoogleAuthUrl, getGoogleStatus } from '@/features/planner'
import { plannerQueryKeys } from '@/features/planner'

function GoogleAccountSection() {
  const queryClient = useQueryClient()
  const [isDisconnectDialogOpen, setIsDisconnectDialogOpen] = useState(false)
  const [isConnecting, setIsConnecting] = useState(false)

  const statusQuery = useQuery({
    queryKey: plannerQueryKeys.googleStatus,
    queryFn: getGoogleStatus,
  })

  const disconnectMutation = useMutation({
    mutationFn: disconnectGoogle,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: plannerQueryKeys.googleStatus })
      setIsDisconnectDialogOpen(false)
    },
  })

  const status = statusQuery.data

  return (
    <>
      <Card variant="outlined">
        <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
          <Stack spacing={2.5}>
            {/* Header row */}
            <Stack direction="row" spacing={1.5} alignItems="center">
              <Avatar
                sx={{
                  width: 40,
                  height: 40,
                  bgcolor: status?.connected ? 'primary.main' : 'action.hover',
                  flexShrink: 0,
                }}
              >
                <GoogleIcon sx={{ fontSize: 20, color: status?.connected ? 'primary.contrastText' : 'text.secondary' }} />
              </Avatar>
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="body1" sx={{ fontWeight: 600 }}>
                  Google Drive
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Link Google Drive files to cards
                </Typography>
              </Box>
            </Stack>

            {/* Loading state */}
            {statusQuery.isLoading && (
              <Stack spacing={1}>
                <Skeleton variant="text" width={200} />
                <Skeleton variant="text" width={140} />
              </Stack>
            )}

            {/* Connected state */}
            {status?.connected && (
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={{ xs: 1.5, sm: 2 }}
                alignItems={{ xs: 'stretch', sm: 'center' }}
                justifyContent="space-between"
                sx={{
                  p: 2,
                  borderRadius: 1,
                  bgcolor: 'action.hover',
                }}
              >
                <Box sx={{ minWidth: 0 }}>
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 600,
                      wordBreak: 'break-all',
                    }}
                  >
                    {status.googleEmail}
                  </Typography>
                  {status.connectedAt ? (
                    <Typography variant="caption" color="text.secondary">
                      Connected on{' '}
                      {(() => {
                        const d = new Date(status.connectedAt)
                        const year = d.getFullYear()
                        const month = String(d.getMonth() + 1).padStart(2, '0')
                        const day = String(d.getDate()).padStart(2, '0')
                        return `${year}-${month}-${day}`
                      })()}
                    </Typography>
                  ) : null}
                </Box>
                <Button
                  color="error"
                  variant="outlined"
                  size="small"
                  startIcon={<LinkOffIcon />}
                  onClick={() => setIsDisconnectDialogOpen(true)}
                  sx={{ flexShrink: 0, alignSelf: { xs: 'flex-start', sm: 'auto' } }}
                >
                  Disconnect
                </Button>
              </Stack>
            )}

            {/* Not connected state */}
            {status && !status.connected && (
              <Box
                sx={{
                  p: 2.5,
                  borderRadius: 1,
                  border: '1px dashed',
                  borderColor: 'divider',
                  textAlign: 'center',
                }}
              >
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                  Connect your Google account to link Drive files to cards and share them with your team.
                </Typography>
                <Button
                  variant="contained"
                  size="small"
                  startIcon={<GoogleIcon />}
                  disabled={isConnecting}
                  onClick={async () => {
                    setIsConnecting(true)
                    try {
                      const url = await getGoogleAuthUrl()
                      window.location.href = url
                    } catch {
                      setIsConnecting(false)
                    }
                  }}
                >
                  {isConnecting ? 'Connecting...' : 'Connect Google Account'}
                </Button>
              </Box>
            )}
          </Stack>
        </CardContent>
      </Card>

      <Dialog
        open={isDisconnectDialogOpen}
        onClose={() => {
          if (!disconnectMutation.isPending) setIsDisconnectDialogOpen(false)
        }}
        maxWidth="xs"
        fullWidth
        aria-labelledby="disconnect-google-dialog-title"
      >
        <DialogTitle id="disconnect-google-dialog-title">
          Disconnect Google Account
        </DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to disconnect your Google account? Existing
            file links will remain visible but you won&apos;t be able to link new
            files.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setIsDisconnectDialogOpen(false)}
            disabled={disconnectMutation.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={() => disconnectMutation.mutate()}
            color="error"
            variant="contained"
            disabled={disconnectMutation.isPending}
          >
            {disconnectMutation.isPending ? 'Disconnecting...' : 'Disconnect'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}

export { GoogleAccountSection }
