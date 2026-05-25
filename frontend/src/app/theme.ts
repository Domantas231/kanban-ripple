import { createTheme } from '@mui/material/styles'
import type { ThemeMode } from '@/stores/uiStore'

function resolveMode(themeMode: ThemeMode): 'light' | 'dark' {
  if (themeMode === 'light' || themeMode === 'dark') return themeMode
  const isDarkModePreferred =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  return isDarkModePreferred ? 'dark' : 'light'
}

const mode = resolveMode((localStorage.getItem('themeMode') as ThemeMode) || 'system')

const palette = {
  light: {
    primary: '#0D7C72',
    secondary: '#334155',
    accent: '#EA580C',
    success: '#15803D',
    warning: '#B45309',
    error: '#B91C1C',
    background: '#F6F8FA',
    surface: '#FFFFFF',
    textPrimary: '#0F172A',
    textSecondary: '#475569',
    textMuted: '#64748B',
    border: '#E2E8F0',
  },
  dark: {
    primary: '#14B8A6',
    secondary: '#2DD4BF',
    accent: '#FB923C',
    success: '#22C55E',
    warning: '#F59E0B',
    error: '#EF4444',
    background: '#0F1419',
    surface: '#181E27',
    textPrimary: '#F1F5F9',
    textSecondary: '#94A3B8',
    textMuted: '#94A3B8',
    border: 'rgba(255,255,255,0.10)',
  },
} as const

const colors = palette[mode]

const shadowBase = mode === 'dark' ? 0.4 : 0.08
const shadows: [
  'none',
  string, string, string, string, string,
  string, string, string, string, string,
  string, string, string, string, string,
  string, string, string, string, string,
  string, string, string, string,
] = [
  'none',
  `0 1px 3px rgba(0,0,0,${shadowBase})`,        // 1: cards
  `0 4px 12px rgba(0,0,0,${shadowBase * 1.5})`, // 2: hover
  `0 8px 24px rgba(0,0,0,${shadowBase * 2})`,   // 3: popovers
  `0 16px 48px rgba(0,0,0,${shadowBase * 3})`,  // 4: dialogs
  'none', 'none', 'none', 'none', 'none',
  'none', 'none', 'none', 'none', 'none',
  'none', 'none', 'none', 'none', 'none',
  'none', 'none', 'none', 'none', 'none',
]

const radiusSm = 4
const radiusMd = 8
const radiusLg = 12
const radiusFull = 9999

export const appTheme = createTheme({
  palette: {
    mode,
    primary: {
      main: colors.primary,
      contrastText: '#FFFFFF',
    },
    secondary: {
      main: colors.secondary,
      contrastText: '#FFFFFF',
    },
    warning: {
      main: colors.accent,
    },
    success: {
      main: colors.success,
    },
    error: {
      main: colors.error,
    },
    background: {
      default: colors.background,
      paper: colors.surface,
    },
    text: {
      primary: colors.textPrimary,
      secondary: colors.textSecondary,
      disabled: colors.textMuted,
    },
    divider: colors.border,
  },

  typography: {
    fontFamily: "'Plus Jakarta Sans', sans-serif",
    h1: {
      fontSize: '2.25rem',
      fontWeight: 700,
      letterSpacing: '-0.02em',
      lineHeight: 1.2,
    },
    h2: {
      fontSize: '1.75rem',
      fontWeight: 600,
      lineHeight: 1.25,
    },
    h3: {
      fontSize: '1.375rem',
      fontWeight: 600,
      lineHeight: 1.3,
    },
    body1: {
      fontSize: '0.9375rem',
      fontWeight: 400,
      lineHeight: 1.6,
    },
    body2: {
      fontSize: '0.875rem',
      lineHeight: 1.5,
    },
    caption: {
      fontSize: '0.8125rem',
      fontWeight: 500,
      lineHeight: 1.4,
    },
    button: {
      fontWeight: 600,
      textTransform: 'none' as const,
    },
  },

  spacing: 8,
  shadows,

  shape: {
    borderRadius: radiusMd,
  },

  transitions: {
    duration: {
      shortest: 100,
      shorter: 150,
      short: 200,
      standard: 200,
      complex: 300,
      enteringScreen: 200,
      leavingScreen: 150,
    },
  },

  zIndex: {
    appBar: 10,
    drawer: 20,
    modal: 40,
    snackbar: 50,
    tooltip: 50,
  },

  components: {
    MuiCssBaseline: {
      styleOverrides: {
        html: {
          height: '100%',
        },
        body: {
          minHeight: '100%',
        },
        '#root': {
          minHeight: '100vh',
        },
        '.skip-to-content': {
          position: 'absolute',
          left: '-9999px',
          top: 'auto',
          width: '1px',
          height: '1px',
          overflow: 'hidden',
          zIndex: 100,
          '&:focus': {
            position: 'fixed',
            top: '8px',
            left: '8px',
            width: 'auto',
            height: 'auto',
            padding: '12px 24px',
            backgroundColor: colors.primary,
            color: '#FFFFFF',
            borderRadius: `${radiusMd}px`,
            fontSize: '0.875rem',
            fontWeight: 600,
            textDecoration: 'none',
            boxShadow: `0 4px 12px rgba(0,0,0,0.2)`,
          },
        },
        '*:focus-visible': {
          outline: `2px solid ${colors.primary}`,
          outlineOffset: '2px',
        },
      },
    },
    MuiButtonBase: {
      defaultProps: {
        disableRipple: true,
        disableTouchRipple: true,
      },
    },
    MuiButton: {
      defaultProps: {
        disableElevation: true,
      },
      styleOverrides: {
        root: {
          borderRadius: radiusMd,
          transition: 'background-color 100ms ease, box-shadow 100ms ease',
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: radiusSm,
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: radiusMd,
          transition: 'box-shadow 150ms ease, transform 150ms ease',
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: radiusLg,
        },
      },
    },
    MuiPopover: {
      styleOverrides: {
        paper: {
          borderRadius: radiusLg,
        },
      },
    },
    MuiAvatar: {
      styleOverrides: {
        root: {
          borderRadius: radiusFull,
        },
      },
    },
    MuiSkeleton: {
      defaultProps: {
        animation: 'wave',
      },
      styleOverrides: {
        root: {
          '&::after': {
            background:
              'linear-gradient(90deg, transparent, rgba(255,255,255,0.08), transparent)',
          },
        },
      },
    },
  },
})

export function createAppTheme(themeMode: ThemeMode) {
  const m = resolveMode(themeMode)
  const c = palette[m]
  const sb = m === 'dark' ? 0.4 : 0.08
  const s: typeof shadows = [
    'none',
    `0 1px 3px rgba(0,0,0,${sb})`,
    `0 4px 12px rgba(0,0,0,${sb * 1.5})`,
    `0 8px 24px rgba(0,0,0,${sb * 2})`,
    `0 16px 48px rgba(0,0,0,${sb * 3})`,
    'none', 'none', 'none', 'none', 'none',
    'none', 'none', 'none', 'none', 'none',
    'none', 'none', 'none', 'none', 'none',
    'none', 'none', 'none', 'none', 'none',
  ]
  return createTheme({
    ...appTheme,
    palette: {
      mode: m,
      primary: { main: c.primary, contrastText: '#FFFFFF' },
      secondary: { main: c.secondary, contrastText: '#FFFFFF' },
      warning: { main: c.accent },
      success: { main: c.success },
      error: { main: c.error },
      background: { default: c.background, paper: c.surface },
      text: { primary: c.textPrimary, secondary: c.textSecondary, disabled: c.textMuted },
      divider: c.border,
    },
    shadows: s,
    components: {
      ...appTheme.components,
      MuiCssBaseline: {
        styleOverrides: {
          ...(appTheme.components?.MuiCssBaseline as Record<string, unknown>)?.styleOverrides as Record<string, unknown>,
          // Override focus-visible color for the resolved mode
          '*:focus-visible': {
            outline: `2px solid ${c.primary}`,
            outlineOffset: '2px',
          },
          '.skip-to-content': {
            position: 'absolute',
            left: '-9999px',
            top: 'auto',
            width: '1px',
            height: '1px',
            overflow: 'hidden',
            zIndex: 100,
            '&:focus': {
              position: 'fixed',
              top: '8px',
              left: '8px',
              width: 'auto',
              height: 'auto',
              padding: '12px 24px',
              backgroundColor: c.primary,
              color: '#FFFFFF',
              borderRadius: `${radiusMd}px`,
              fontSize: '0.875rem',
              fontWeight: 600,
              textDecoration: 'none',
              boxShadow: '0 4px 12px rgba(0,0,0,0.2)',
            },
          },
        },
      },
    },
  })
}

