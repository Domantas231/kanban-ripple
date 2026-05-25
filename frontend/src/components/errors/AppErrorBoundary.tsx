import React, { type ErrorInfo, type ReactNode } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline'
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined'
import RefreshIcon from '@mui/icons-material/Refresh'

interface AppErrorBoundaryProps {
  children: ReactNode
}

interface AppErrorBoundaryState {
  hasError: boolean
}

export class AppErrorBoundary extends React.Component<
  AppErrorBoundaryProps,
  AppErrorBoundaryState
> {
  public override state: AppErrorBoundaryState = {
    hasError: false,
  }

  public static getDerivedStateFromError(): AppErrorBoundaryState {
    return { hasError: true }
  }

  public override componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error('Unhandled UI error caught by AppErrorBoundary', error, errorInfo)
  }

  private handleRetry = () => {
    this.setState({ hasError: false })
  }

  private handleGoHome = () => {
    window.location.assign('/')
  }

  public override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <Box
          sx={{
            minHeight: '60vh',
            display: 'grid',
            placeItems: 'center',
            px: 2,
          }}
        >
          <Stack spacing={2.5} alignItems="center" textAlign="center" maxWidth={480}>
            <Box
              sx={{
                width: 80,
                height: 80,
                borderRadius: '50%',
                bgcolor: (theme) =>
                  theme.palette.mode === 'dark'
                    ? 'rgba(239, 68, 68, 0.12)'
                    : 'rgba(220, 38, 38, 0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <ErrorOutlineIcon sx={{ fontSize: 40, color: 'error.main' }} />
            </Box>
            <Typography variant="h5" component="h1">
              Something went wrong
            </Typography>
            <Typography color="text.secondary" variant="body2">
              An unexpected error happened while rendering this page. You can try again or go back to the home page.
            </Typography>
            <Stack direction="row" spacing={1.5}>
              <Button
                variant="contained"
                startIcon={<RefreshIcon />}
                onClick={this.handleRetry}
              >
                Try Again
              </Button>
              <Button
                variant="outlined"
                startIcon={<HomeOutlinedIcon />}
                onClick={this.handleGoHome}
              >
                Go Home
              </Button>
            </Stack>
          </Stack>
        </Box>
      )
    }

    return this.props.children
  }
}
