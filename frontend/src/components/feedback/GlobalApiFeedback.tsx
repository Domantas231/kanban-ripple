import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import SyncProblemOutlinedIcon from '@mui/icons-material/SyncProblemOutlined'
import { useEffect, useState } from 'react'
import type { SyntheticEvent } from 'react'
import { useUiStore } from '@/stores/uiStore'
import type { ToastMessage } from '@/stores/uiStore'

export function GlobalApiFeedback() {
  const activeToast = useUiStore((state) => state.activeToast)
  const dismissToast = useUiStore((state) => state.dismissToast)
  const conflictDialogOpen = useUiStore((state) => state.conflictDialogOpen)
  const conflictDialogMessage = useUiStore((state) => state.conflictDialogMessage)
  const closeConflictDialog = useUiStore((state) => state.closeConflictDialog)

  // Snapshot the toast so its content (severity, message) survives the
  // Snackbar's exit animation. Reading `activeToast` directly would flip the
  // Alert to the fallback 'error' severity (red) the moment it's dismissed,
  // because MUI keeps the Alert mounted while it animates out.
  const [displayedToast, setDisplayedToast] = useState<ToastMessage | null>(activeToast)

  useEffect(() => {
    if (activeToast) {
      setDisplayedToast(activeToast)
    }
  }, [activeToast])

  const handleToastExited = () => {
    setDisplayedToast(null)
  }

  const handleToastClose = (
    _event?: Event | SyntheticEvent,
    reason?: string,
  ) => {
    if (reason === 'clickaway') {
      return
    }

    dismissToast()
  }

  const handleRefreshPage = () => {
    closeConflictDialog()

    if (typeof window !== 'undefined') {
      window.location.reload()
    }
  }

  return (
    <>
      <Snackbar
        open={Boolean(activeToast)}
        autoHideDuration={displayedToast?.durationMs}
        onClose={handleToastClose}
        TransitionProps={{ onExited: handleToastExited }}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert
          variant="filled"
          onClose={handleToastClose}
          severity={displayedToast?.severity ?? 'info'}
          sx={{
            width: '100%',
            minWidth: { xs: 0, sm: 320 },
            maxWidth: { xs: 'calc(100vw - 32px)', sm: 'none' },
            borderRadius: 2,
            boxShadow: 3,
          }}
        >
          {displayedToast?.message ?? ''}
        </Alert>
      </Snackbar>

      <Dialog
        open={conflictDialogOpen}
        onClose={closeConflictDialog}
        maxWidth="xs"
        fullWidth
        aria-labelledby="conflict-dialog-title"
        aria-describedby="conflict-dialog-description"
      >
        <DialogTitle id="conflict-dialog-title">
          <Stack direction="row" alignItems="center" spacing={1.5}>
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: '50%',
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark'
                    ? 'rgba(251, 146, 60, 0.15)'
                    : 'rgba(249, 115, 22, 0.1)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}
            >
              <SyncProblemOutlinedIcon sx={{ color: 'warning.main' }} />
            </Box>
            <Typography variant="h6" component="span">
              Version Conflict
            </Typography>
          </Stack>
        </DialogTitle>
        <DialogContent>
          <Typography id="conflict-dialog-description" variant="body2" color="text.secondary">
            {conflictDialogMessage}
          </Typography>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={closeConflictDialog}>Dismiss</Button>
          <Button onClick={handleRefreshPage} variant="contained">
            Refresh
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
