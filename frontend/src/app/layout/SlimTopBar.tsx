import { useNavigate, useRouterState } from '@tanstack/react-router'
import AppBar from '@mui/material/AppBar'
import Box from '@mui/material/Box'
import Breadcrumbs from '@mui/material/Breadcrumbs'
import IconButton from '@mui/material/IconButton'
import Link from '@mui/material/Link'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import MenuIcon from '@mui/icons-material/Menu'
import NavigateNextIcon from '@mui/icons-material/NavigateNext'
import { GlobalSearchBar } from '@/features/search'
import { useBoard } from '@/features/boards'
import { useProject } from '@/features/projects'
import { useUiStore } from '@/stores/uiStore'
import { SIDEBAR_WIDTH_EXPANDED, SIDEBAR_WIDTH_COLLAPSED } from './Sidebar'

export const TOPBAR_HEIGHT = 48

export function SlimTopBar() {
  const navigate = useNavigate()
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const collapsed = useUiStore((state) => state.sidebarCollapsed)
  const setMobileOpen = useUiStore((state) => state.setMobileDrawerOpen)

  const projectId = pathname.match(/^\/projects\/([^/]+)/)?.[1]
  const boardId = pathname.match(/\/boards\/([^/]+)/)?.[1]
  const isOnProject = Boolean(projectId) && pathname !== '/projects'
  const projectQuery = useProject(isOnProject ? projectId : undefined)
  const boardQuery = useBoard(boardId)

  const sidebarWidth = collapsed ? SIDEBAR_WIDTH_COLLAPSED : SIDEBAR_WIDTH_EXPANDED

  const breadcrumbs = buildBreadcrumbs(pathname, projectQuery.data?.name, projectId, boardQuery.data?.name)

  return (
    <AppBar
      position="fixed"
      color="transparent"
      elevation={0}
      sx={{
        width: { md: `calc(100% - ${sidebarWidth}px)` },
        ml: { md: `${sidebarWidth}px` },
        transition: 'width 200ms ease, margin-left 200ms ease',
        height: TOPBAR_HEIGHT,
        bgcolor: 'background.default',
        borderBottom: 1,
        borderColor: 'divider',
        zIndex: (theme) => theme.zIndex.appBar,
      }}
    >
      <Toolbar
        disableGutters
        sx={{
          minHeight: `${TOPBAR_HEIGHT}px !important`,
          height: TOPBAR_HEIGHT,
          px: { xs: 1.5, sm: 2, md: 3 },
          gap: 1,
        }}
      >
        {/* Mobile hamburger */}
        <IconButton
          aria-label="Open navigation"
          onClick={() => setMobileOpen(true)}
          sx={{ display: { md: 'none' }, mr: 0.5 }}
          size="small"
        >
          <MenuIcon fontSize="small" />
        </IconButton>

        {/* Breadcrumbs */}
        <Breadcrumbs
          separator={<NavigateNextIcon sx={{ fontSize: 18, display: 'block', color: 'text.secondary' }} />}
          sx={{
            flex: '0 1 auto',
            minWidth: 0,
            display: 'flex',
            alignItems: 'center',
            '& .MuiBreadcrumbs-ol': { flexWrap: 'nowrap', alignItems: 'center' },
            '& .MuiBreadcrumbs-li': { display: 'flex', alignItems: 'center' },
            '& .MuiBreadcrumbs-separator': {
              display: { xs: 'none', sm: 'flex' },
              alignItems: 'center',
              height: '1.43em',
              mx: 0.75,
              mt: '-1px',
            },
          }}
        >
          {breadcrumbs.map((crumb, i) => {
            const isLast = i === breadcrumbs.length - 1
            if (isLast) {
              return (
                <Typography
                  key={crumb.path}
                  variant="body2"
                  component="span"
                  color="text.primary"
                  noWrap
                  sx={{ fontWeight: 600, lineHeight: 1.43, display: 'inline-flex', alignItems: 'center' }}
                >
                  {crumb.label}
                </Typography>
              )
            }
            return (
              <Link
                key={crumb.path}
                component="button"
                variant="body2"
                color="text.secondary"
                underline="hover"
                onClick={() => navigate({ to: crumb.path })}
                sx={{
                  cursor: 'pointer',
                  whiteSpace: 'nowrap',
                  p: 0,
                  border: 0,
                  bgcolor: 'transparent',
                  lineHeight: 1.43,
                  display: { xs: 'none', sm: 'inline-flex' },
                  alignItems: 'center',
                  font: 'inherit',
                  fontSize: '0.875rem',
                }}
              >
                {crumb.label}
              </Link>
            )
          })}
        </Breadcrumbs>

        <Box sx={{ flexGrow: 1 }} />

        {/* Search trigger */}
        <Box sx={{ width: { xs: 'auto', sm: 200, md: 280 } }}>
          <GlobalSearchBar />
        </Box>
      </Toolbar>
    </AppBar>
  )
}

type Crumb = { label: string; path: string }

function buildBreadcrumbs(
  pathname: string,
  projectName: string | undefined,
  projectId: string | undefined,
  boardName: string | undefined,
): Crumb[] {
  const crumbs: Crumb[] = []

  if (pathname === '/projects') {
    crumbs.push({ label: 'Workspaces', path: '/projects' })
    return crumbs
  }

  if (pathname === '/settings') {
    crumbs.push({ label: 'Account Settings', path: '/settings' })
    return crumbs
  }

  if (pathname === '/archive') {
    crumbs.push({ label: 'Archive', path: '/archive' })
    return crumbs
  }

  if (projectId) {
    crumbs.push({ label: 'Workspaces', path: '/projects' })
    const name = projectName ?? 'Project'

    if (pathname === `/projects/${projectId}`) {
      crumbs.push({ label: name, path: `/projects/${projectId}` })
    } else if (pathname === `/projects/${projectId}/swimlane`) {
      crumbs.push({ label: name, path: `/projects/${projectId}` })
      crumbs.push({ label: 'Overview', path: `/projects/${projectId}/swimlane` })
    } else if (pathname === `/projects/${projectId}/gantt`) {
      crumbs.push({ label: name, path: `/projects/${projectId}` })
      crumbs.push({ label: 'Timeline', path: `/projects/${projectId}/gantt` })
    } else if (pathname === `/projects/${projectId}/settings`) {
      crumbs.push({ label: name, path: `/projects/${projectId}` })
      crumbs.push({ label: 'Settings', path: `/projects/${projectId}/settings` })
    } else {
      const boardMatch = pathname.match(/\/boards\/([^/]+)/)
      crumbs.push({ label: name, path: `/projects/${projectId}` })
      if (boardMatch) {
        crumbs.push({ label: boardName ?? 'Board', path: pathname })
      }
    }
  }

  return crumbs.length > 0 ? crumbs : [{ label: 'Home', path: '/projects' }]
}
