import { useMemo, useState } from 'react'
import { useNavigate, useRouterState } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Divider from '@mui/material/Divider'
import Drawer from '@mui/material/Drawer'
import SwipeableDrawer from '@mui/material/SwipeableDrawer'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Skeleton from '@mui/material/Skeleton'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import CloseIcon from '@mui/icons-material/Close'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import LogoutIcon from '@mui/icons-material/Logout'
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined'
import TableChartOutlinedIcon from '@mui/icons-material/TableChartOutlined'
import TimelineOutlinedIcon from '@mui/icons-material/TimelineOutlined'
import CalendarTodayOutlinedIcon from '@mui/icons-material/CalendarTodayOutlined'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import type { ProjectRole } from '@/lib/types'
import rippleLogo from '@/assets/ripple_logo.svg'
import Badge from '@mui/material/Badge'
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone'
import { NotificationList } from '@/features/notifications'
import { useUnreadCount } from '@/features/notifications'
import { logout } from '@/features/auth'
import { getGoogleStatus } from '@/features/planner'
import {
  isManagerPlus,
  isMemberPlus,
  useProject,
  useProjectMembers,
  useProjects,
} from '@/features/projects'
import { plannerQueryKeys } from '@/features/planner'
import { useAuthStore } from '@/features/auth'
import { useUiStore } from '@/stores/uiStore'

export const SIDEBAR_WIDTH_EXPANDED = 256
export const SIDEBAR_WIDTH_COLLAPSED = 64

const iOS = typeof navigator !== 'undefined' && /iPad|iPhone|iPod/.test(navigator.userAgent)

type NavItem = {
  label: string
  icon: React.ReactNode
  path: string
  match: (pathname: string, projectId: string) => boolean
}

function getProjectNavItems(projectId: string, canSeeOverview: boolean): NavItem[] {
  const items: NavItem[] = [
    {
      label: 'Dashboard',
      icon: <DashboardOutlinedIcon fontSize="small" />,
      path: `/projects/${projectId}`,
      match: (p, id) => p === `/projects/${id}`,
    },
  ]

  if (canSeeOverview) {
    items.push({
      label: 'Overview',
      icon: <TableChartOutlinedIcon fontSize="small" />,
      path: `/projects/${projectId}/swimlane`,
      match: (p, id) => p === `/projects/${id}/swimlane`,
    })
  }

  items.push({
    label: 'Timeline',
    icon: <TimelineOutlinedIcon fontSize="small" />,
    path: `/projects/${projectId}/gantt`,
    match: (p, id) => p === `/projects/${id}/gantt`,
  })

  return items
}

export function Sidebar() {
  const navigate = useNavigate()
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const collapsed = useUiStore((state) => state.sidebarCollapsed)
  const toggleCollapsed = useUiStore((state) => state.toggleSidebarCollapsed)
  const mobileOpen = useUiStore((state) => state.mobileDrawerOpen)
  const setMobileOpen = useUiStore((state) => state.setMobileDrawerOpen)
  const setBoardArchiveDrawerOpen = useUiStore((state) => state.setBoardArchiveDrawerOpen)
  const boardPlannerOpen = useUiStore((state) => state.boardPlannerOpen)
  const toggleBoardPlanner = useUiStore((state) => state.toggleBoardPlanner)
  const user = useAuthStore((state) => state.user)
  const clearAuth = useAuthStore((state) => state.clearAuth)

  const projectId = pathname.match(/^\/projects\/([^/]+)/)?.[1]
  const boardId = pathname.match(/^\/projects\/[^/]+\/boards\/([^/]+)/)?.[1]
  const isOnBoard = Boolean(boardId)
  const isOnProject = Boolean(projectId) && pathname !== '/projects'
  const projectQuery = useProject(isOnProject ? projectId : undefined)
  const membersQuery = useProjectMembers(isOnProject ? projectId : undefined)
  const projectsQuery = useProjects()
  const projects = projectsQuery.data?.items ?? []

  const currentUserRole = useMemo<ProjectRole | undefined>(() => {
    if (!user?.id || !isOnProject) return undefined
    if (projectQuery.data?.ownerId === user.id) return 0 as ProjectRole
    return membersQuery.data?.find((member) => member.userId === user.id)?.role
  }, [user?.id, isOnProject, projectQuery.data?.ownerId, membersQuery.data])
  const canSeeOverview = isManagerPlus(currentUserRole)
  const canSeePlanner = isMemberPlus(currentUserRole)
  const canSeeBoardArchive = isMemberPlus(currentUserRole)

  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const unreadCountQuery = useUnreadCount()
  const unreadCount = unreadCountQuery.data ?? 0

  const googleStatusQuery = useQuery({
    queryKey: plannerQueryKeys.googleStatus,
    queryFn: getGoogleStatus,
    staleTime: 5 * 60 * 1000,
  })
  const googleConnected = googleStatusQuery.data?.connected ?? false

  const width = collapsed ? SIDEBAR_WIDTH_COLLAPSED : SIDEBAR_WIDTH_EXPANDED

  const handleLogout = async () => {
    try {
      await logout()
    } finally {
      clearAuth()
      navigate({ to: '/login' })
    }
  }

  const handleNavClick = (path: string) => {
    setMobileOpen(false)
    navigate({ to: path })
  }

  const drawerContent = (
    <Box
      component="aside"
      sx={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        bgcolor: 'background.paper',
        borderRight: 1,
        borderColor: 'divider',
        overflow: 'hidden',
      }}
    >
      {/* Logo + App name */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: { xs: 'flex-start', md: collapsed ? 'center' : 'flex-start' },
          gap: 1.5,
          px: collapsed ? { xs: 2, md: 0 } : 2,
          height: { xs: 56, md: 48 },
          flexShrink: 0,
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Box
          onClick={() => handleNavClick('/projects')}
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            flex: 1,
            minWidth: 0,
            cursor: 'pointer',
            justifyContent: { xs: 'flex-start', md: collapsed ? 'center' : 'flex-start' },
          }}
        >
          <Box
            component="img"
            src={rippleLogo}
            alt="Ripple logo"
            sx={{ width: 28, height: 28, flexShrink: 0 }}
          />
          <Typography
            variant="body1"
            sx={{
              fontWeight: 700,
              whiteSpace: 'nowrap',
              display: { xs: 'block', md: collapsed ? 'none' : 'block' },
            }}
          >
            Kanban Ripple
          </Typography>
        </Box>
        <IconButton
          aria-label="Close navigation"
          onClick={() => setMobileOpen(false)}
          sx={{
            display: { xs: 'inline-flex', md: 'none' },
            ml: 'auto',
            minWidth: 44,
            minHeight: 44,
          }}
        >
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>

      {/* Project switcher: hidden when collapsed on desktop, always shown on mobile */}
      <Box
        sx={{
          px: 1.5,
          py: 1.5,
          display: { xs: 'block', md: collapsed ? 'none' : 'block' },
        }}
      >
        {projectsQuery.isLoading ? (
            <Skeleton variant="rounded" height={40} />
          ) : (
            <Select
              size="small"
              fullWidth
              displayEmpty
              value={isOnProject && projectId ? projectId : ''}
              onChange={(e) => {
                const val = e.target.value as string
                if (val) {
                  handleNavClick(`/projects/${val}`)
                } else {
                  handleNavClick('/projects')
                }
              }}
              renderValue={(selected) => {
                if (!selected)
                  return (
                    <Typography variant="body2" color="text.secondary">
                      Select workspace
                    </Typography>
                  )
                const proj = projects.find((p) => p.id === selected)
                return (
                  <Typography variant="body2" noWrap>
                    {proj?.name ?? 'Workspace'}
                  </Typography>
                )
              }}
              sx={{ '& .MuiSelect-select': { py: 1 } }}
            >
              <MenuItem value="">
                <Typography variant="body2" color="text.secondary">
                  All workspaces
                </Typography>
              </MenuItem>
              {projects.map((p) => (
                <MenuItem key={p.id} value={p.id}>
                  <Typography variant="body2" noWrap>
                    {p.name}
                  </Typography>
                </MenuItem>
              ))}
          </Select>
        )}
      </Box>

      {/* Primary nav: project-specific when on a project */}
      <List sx={{ flex: 1, px: { xs: 1, md: collapsed ? 0.5 : 1 }, py: 0.5 }}>
        {isOnProject && projectId
          ? getProjectNavItems(projectId, canSeeOverview).map((item) => {
              const active = item.match(pathname, projectId)
              return (
                <NavListItem
                  key={item.label}
                  label={item.label}
                  icon={item.icon}
                  active={active}
                  collapsed={collapsed}
                  onClick={() => handleNavClick(item.path)}
                />
              )
            })
          : null}

        {isOnBoard && projectId && boardId ? (
          <>
            {canSeePlanner ? (
              <NavListItem
                label="Planner"
                icon={<CalendarTodayOutlinedIcon fontSize="small" />}
                active={boardPlannerOpen && googleConnected}
                collapsed={collapsed}
                onClick={() => toggleBoardPlanner()}
                disabled={!googleConnected}
                disabledTooltip="Connect your Google account in settings to use the Planner."
              />
            ) : null}
            {canSeeBoardArchive ? (
              <NavListItem
                label="Archive"
                icon={<ArchiveOutlinedIcon fontSize="small" />}
                active={false}
                collapsed={collapsed}
                onClick={() => setBoardArchiveDrawerOpen(true)}
              />
            ) : null}
          </>
        ) : null}
      </List>

      <Divider />

      {/* Secondary nav */}
      <List sx={{ px: { xs: 1, md: collapsed ? 0.5 : 1 }, py: 0.5 }}>
        <NavListItem
          label="Notifications"
          icon={
            <Badge
              badgeContent={unreadCount > 3 ? unreadCount : undefined}
              variant={unreadCount > 0 && unreadCount <= 3 ? 'dot' : 'standard'}
              color="primary"
              max={99}
              invisible={unreadCount === 0}
            >
              <NotificationsNoneIcon fontSize="small" />
            </Badge>
          }
          active={false}
          collapsed={collapsed}
          onClick={() => setNotificationsOpen(true)}
        />
        {isOnProject && projectId && (
          <NavListItem
            label="Workspace settings"
            icon={<SettingsOutlinedIcon fontSize="small" />}
            active={pathname === `/projects/${projectId}/settings`}
            collapsed={collapsed}
            onClick={() => handleNavClick(`/projects/${projectId}/settings`)}
          />
        )}
      </List>

      <Divider />

      {/* User section */}
      <Box
        sx={{
          px: collapsed ? { xs: 1.5, md: 0 } : 1.5,
          py: 1.5,
          display: 'flex',
          alignItems: 'center',
          justifyContent: { xs: 'flex-start', md: collapsed ? 'center' : 'flex-start' },
          gap: 1,
        }}
      >
        <Tooltip title={collapsed ? 'Account settings' : ''} placement="right">
          <Box
            onClick={() => handleNavClick('/settings')}
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              flex: 1,
              overflow: 'hidden',
              cursor: 'pointer',
              borderRadius: 1,
              px: { xs: 0.75, md: collapsed ? 0 : 0.5 },
              py: { xs: 0.75, md: 0.5 },
              minHeight: { xs: 48, md: 'auto' },
              justifyContent: { xs: 'flex-start', md: collapsed ? 'center' : 'flex-start' },
              '&:hover': { bgcolor: 'action.hover' },
            }}
          >
            <UserAvatar
              userId={user?.id}
              name={user?.userName ?? user?.email}
              sx={{
                width: { xs: 36, md: 32 },
                height: { xs: 36, md: 32 },
                fontSize: '0.875rem',
                bgcolor: 'primary.main',
                flexShrink: 0,
              }}
            />
            <Box
              sx={{
                flex: 1,
                overflow: 'hidden',
                display: { xs: 'block', md: collapsed ? 'none' : 'block' },
              }}
            >
              <Typography variant="body2" noWrap sx={{ fontWeight: 600 }}>
                {user?.userName ?? user?.email ?? 'User'}
              </Typography>
              <Typography variant="caption" color="text.secondary" noWrap>
                {user?.email}
              </Typography>
            </Box>
          </Box>
        </Tooltip>
        <Tooltip title="Logout">
          <IconButton
            onClick={handleLogout}
            aria-label="Logout"
            sx={{
              display: { xs: 'inline-flex', md: collapsed ? 'none' : 'inline-flex' },
              minWidth: { xs: 44, md: 36 },
              minHeight: { xs: 44, md: 36 },
            }}
          >
            <LogoutIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>

      {/* Collapse toggle */}
      <Box
        sx={{
          display: { xs: 'none', md: 'flex' },
          justifyContent: 'center',
          px: 1,
          pb: 1,
        }}
      >
        <Tooltip title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}>
          <IconButton size="small" onClick={toggleCollapsed} aria-label="Toggle sidebar">
            {collapsed ? (
              <ChevronRightIcon fontSize="small" />
            ) : (
              <ChevronLeftIcon fontSize="small" />
            )}
          </IconButton>
        </Tooltip>
      </Box>
    </Box>
  )

  return (
    <>
      {/* Desktop permanent drawer */}
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: 'none', md: 'block' },
          width,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width,
            boxSizing: 'border-box',
            borderRight: 'none',
            transition: 'width 200ms ease',
            overflowX: 'hidden',
          },
        }}
      >
        {drawerContent}
      </Drawer>

      {/* Mobile swipeable drawer */}
      <SwipeableDrawer
        variant="temporary"
        open={mobileOpen}
        onOpen={() => setMobileOpen(true)}
        onClose={() => setMobileOpen(false)}
        disableBackdropTransition={!iOS}
        disableDiscovery={iOS}
        ModalProps={{ keepMounted: true }}
        swipeAreaWidth={20}
        sx={{
          display: { xs: 'block', md: 'none' },
          '& .MuiDrawer-paper': {
            width: 'min(85vw, 320px)',
            boxSizing: 'border-box',
          },
        }}
      >
        {drawerContent}
      </SwipeableDrawer>

      {/* Notifications drawer */}
      <Drawer
        anchor="right"
        open={notificationsOpen}
        onClose={() => setNotificationsOpen(false)}
        slotProps={{
          paper: {
            sx: {
              width: { xs: '100vw', sm: 400 },
              maxWidth: '100vw',
            },
          },
        }}
      >
        <NotificationList onNavigate={() => setNotificationsOpen(false)} />
      </Drawer>
    </>
  )
}

type NavListItemProps = {
  label: string
  icon: React.ReactNode
  active: boolean
  collapsed: boolean
  onClick: () => void
  disabled?: boolean
  disabledTooltip?: string
}

function NavListItem({
  label,
  icon,
  active,
  collapsed,
  onClick,
  disabled = false,
  disabledTooltip,
}: NavListItemProps) {
  const button = (
    <ListItemButton
      onClick={disabled ? undefined : onClick}
      disabled={disabled}
      sx={{
        borderRadius: 1,
        mb: { xs: 0.5, md: 0.25 },
        minHeight: { xs: 48, md: 40 },
        justifyContent: { xs: 'flex-start', md: collapsed ? 'center' : 'flex-start' },
        px: { xs: 1.5, md: collapsed ? 1 : 1.5 },
        position: 'relative',
        bgcolor: active ? 'action.selected' : 'transparent',
        '&::before': active
          ? {
              content: '""',
              position: 'absolute',
              left: 0,
              top: 4,
              bottom: 4,
              width: 3,
              borderRadius: 2,
              bgcolor: 'primary.main',
            }
          : undefined,
        '&:hover': {
          bgcolor: active ? 'action.selected' : 'action.hover',
        },
        '&.Mui-disabled': {
          opacity: 0.5,
          pointerEvents: 'auto',
          cursor: 'not-allowed',
        },
      }}
    >
      <ListItemIcon
        sx={{
          minWidth: { xs: 36, md: collapsed ? 0 : 36 },
          justifyContent: 'center',
          color: disabled ? 'text.disabled' : active ? 'primary.main' : 'text.secondary',
        }}
      >
        {icon}
      </ListItemIcon>
      <ListItemText
        primary={label}
        sx={{ display: { xs: 'block', md: collapsed ? 'none' : 'block' } }}
        primaryTypographyProps={{
          variant: 'body2',
          fontWeight: active ? 600 : 400,
          color: disabled ? 'text.disabled' : active ? 'text.primary' : 'text.secondary',
          noWrap: true,
        }}
      />
    </ListItemButton>
  )

  const tooltipTitle = disabled && disabledTooltip ? disabledTooltip : collapsed ? label : ''
  if (tooltipTitle) {
    return (
      <Tooltip title={tooltipTitle} placement="right">
        <Box>{button}</Box>
      </Tooltip>
    )
  }
  return button
}
