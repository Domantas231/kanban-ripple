import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone'
import Badge from '@mui/material/Badge'
import SwipeableDrawer from '@mui/material/SwipeableDrawer'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import { useState } from 'react'
import { useUnreadCount } from '@/features/notifications/api/notifications'
import { NotificationList } from './NotificationList'

export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const unreadCountQuery = useUnreadCount()
  const unreadCount = unreadCountQuery.data ?? 0

  const showDot = unreadCount > 0 && unreadCount <= 3
  const showNumber = unreadCount > 3

  return (
    <>
      <Tooltip title="Notifications">
        <IconButton
          color="inherit"
          size="small"
          onClick={() => setOpen(true)}
          aria-label="Open notifications"
          aria-expanded={open ? 'true' : undefined}
          aria-haspopup="true"
        >
          <Badge
            badgeContent={showNumber ? unreadCount : undefined}
            variant={showDot ? 'dot' : 'standard'}
            color="primary"
            max={99}
            invisible={unreadCount === 0}
          >
            <NotificationsNoneIcon fontSize="small" />
          </Badge>
        </IconButton>
      </Tooltip>

      <SwipeableDrawer
        anchor="right"
        open={open}
        onOpen={() => setOpen(true)}
        onClose={() => setOpen(false)}
        disableSwipeToOpen
        swipeAreaWidth={0}
        slotProps={{
          paper: {
            sx: {
              width: { xs: '100vw', sm: 400 },
              maxWidth: '100vw',
            },
          },
        }}
      >
        <NotificationList onNavigate={() => setOpen(false)} />
      </SwipeableDrawer>
    </>
  )
}
