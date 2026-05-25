import type { ReactNode } from 'react'
import { Link as RouterLink } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import Paper from '@mui/material/Paper'
import rippleLogo from '@/assets/ripple_logo.svg'

type AuthLayoutProps = {
  children: ReactNode
}

export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        minHeight: '100vh',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        bgcolor: 'background.default',
        px: { xs: 2, sm: 4, md: 6 },
        py: 3,
      }}
    >
      {/* Centered form area */}
      <Box
        sx={{
          width: '100%',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        {/* Form card */}
        <Paper
          elevation={0}
          sx={{
            width: '100%',
            maxWidth: 420,
            p: { xs: 3, sm: 4 },
            border: 1,
            borderColor: 'divider',
            borderRadius: 2,
            '@keyframes fadeUp': {
              from: {
                opacity: 0,
                transform: 'translateY(12px)',
              },
              to: {
                opacity: 1,
                transform: 'translateY(0)',
              },
            },
            animation: 'fadeUp 400ms ease-out',
          }}
        >
          {/* Logo */}
          <Box
            component={RouterLink}
            to="/"
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 1,
              textDecoration: 'none',
              color: 'text.primary',
              mb: 3,
            }}
          >
            <Box component="img" src={rippleLogo} alt="Ripple logo" sx={{ width: 28, height: 28 }} />
            <Typography variant="h6" sx={{ fontWeight: 700, letterSpacing: '-0.01em' }}>
              Kanban Ripple
            </Typography>
          </Box>
          {children}
        </Paper>
      </Box>
    </Box>
  )
}
