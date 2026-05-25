import type { ReactNode } from 'react'
import { useMemo, useSyncExternalStore } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { CssBaseline, ThemeProvider } from '@mui/material'
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider'
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns'
import { queryClient } from '@/lib/query-client'
import { useUiStore } from '@/stores/uiStore'
import { createAppTheme } from './theme'

const darkMq =
  typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null

function useSystemDarkMode() {
  return useSyncExternalStore(
    (cb) => {
      darkMq?.addEventListener('change', cb)
      return () => darkMq?.removeEventListener('change', cb)
    },
    () => darkMq?.matches ?? false,
  )
}

interface AppProviderProps {
  children: ReactNode
}

export function AppProvider({ children }: AppProviderProps) {
  const themeMode = useUiStore((s) => s.themeMode)
  const systemDark = useSystemDarkMode()
  const theme = useMemo(() => {
    if (themeMode === 'system') {
      return systemDark ? createAppTheme('dark') : createAppTheme('light')
    }
    return createAppTheme(themeMode)
  }, [themeMode, systemDark])

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <LocalizationProvider dateAdapter={AdapterDateFns}>
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </LocalizationProvider>
    </ThemeProvider>
  )
}
