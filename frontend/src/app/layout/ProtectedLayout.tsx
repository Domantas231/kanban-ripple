import { Outlet } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import { Sidebar, SIDEBAR_WIDTH_EXPANDED, SIDEBAR_WIDTH_COLLAPSED } from './Sidebar'
import { SlimTopBar, TOPBAR_HEIGHT } from './SlimTopBar'
import { useUiStore } from '@/stores/uiStore'

export function ProtectedLayout() {
  const collapsed = useUiStore((state) => state.sidebarCollapsed)
  const sidebarWidth = collapsed ? SIDEBAR_WIDTH_COLLAPSED : SIDEBAR_WIDTH_EXPANDED

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <a className="skip-to-content" href="#main-content">
        Skip to content
      </a>
      <Sidebar />
      <SlimTopBar />
      <Box
        id="main-content"
        component="main"
        tabIndex={-1}
        sx={{
          flexGrow: 1,
          width: { xs: '100%', md: `calc(100% - ${sidebarWidth}px)` },
          pt: `${TOPBAR_HEIGHT}px`,
          px: 0,
          pb: 0,
          transition: 'width 200ms ease',
          overflowX: 'hidden',
          overflowY: 'auto',
        }}
      >
        <Outlet />
      </Box>
    </Box>
  )
}
