import AlternateEmailIcon from '@mui/icons-material/AlternateEmail'
import AssignmentIndOutlinedIcon from '@mui/icons-material/AssignmentIndOutlined'
import CloseIcon from '@mui/icons-material/Close'
import DeleteSweepOutlinedIcon from '@mui/icons-material/DeleteSweepOutlined'
import DriveFileMoveOutlinedIcon from '@mui/icons-material/DriveFileMoveOutlined'
import EditOutlinedIcon from '@mui/icons-material/EditOutlined'
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown'
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp'
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone'
import NotificationsOffOutlinedIcon from '@mui/icons-material/NotificationsOffOutlined'
import PostAddOutlinedIcon from '@mui/icons-material/PostAddOutlined'
import Alert from '@mui/material/Alert'
import { UserAvatar } from '@/features/auth'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Typography from '@mui/material/Typography'
import { useTheme } from '@mui/material/styles'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from 'react'
import { boardsQueryKeys, getBoard } from '@/features/boards'
import { cardsQueryKeys, getCard } from '@/features/cards'
import {
  useDeleteNotification,
  useInfiniteNotifications,
  useMarkAllAsRead,
  useMarkAsRead,
} from '@/features/notifications/api/notifications'
import { projectsQueryKeys } from '@/features/projects'
import type { Guid, Notification, NotificationType } from '@/lib/types'

type NotificationListProps = {
  onNavigate?: () => void
}

type TabValue = 'all' | 'unread'

type RouteTarget =
  | { to: '/projects' }
  | { to: '/projects/$projectId'; params: { projectId: Guid } }
  | {
      to: '/projects/$projectId/boards/$boardId'
      params: { projectId: Guid; boardId: Guid }
      search?: { cardId?: Guid }
    }

export function NotificationList({ onNavigate }: NotificationListProps) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<TabValue>('unread')
  const [busyNotificationId, setBusyNotificationId] = useState<Guid | null>(null)
  const [navigationError, setNavigationError] = useState<string | null>(null)

  const infiniteQuery = useInfiniteNotifications()
  const markAsReadMutation = useMarkAsRead()
  const markAllAsReadMutation = useMarkAllAsRead()
  const deleteNotificationMutation = useDeleteNotification()

  const allNotifications = useMemo(
    () =>
      (infiniteQuery.data?.pages ?? [])
        .flatMap((page) => page.items)
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()),
    [infiniteQuery.data?.pages],
  )

  const filteredNotifications = useMemo(
    () => (tab === 'unread' ? allNotifications.filter((n) => !n.isRead) : allNotifications),
    [allNotifications, tab],
  )

  const unreadCount = useMemo(
    () => allNotifications.filter((n) => !n.isRead).length,
    [allNotifications],
  )

  // Infinite scroll observer
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const observerRef = useRef<IntersectionObserver | null>(null)

  const handleSentinel = useCallback(
    (node: HTMLDivElement | null) => {
      if (observerRef.current) {
        observerRef.current.disconnect()
      }
      sentinelRef.current = node
      if (!node) return

      observerRef.current = new IntersectionObserver(
        (entries) => {
          if (entries[0]?.isIntersecting && infiniteQuery.hasNextPage && !infiniteQuery.isFetchingNextPage) {
            void infiniteQuery.fetchNextPage()
          }
        },
        { threshold: 0.1 },
      )
      observerRef.current.observe(node)
    },
    [infiniteQuery.hasNextPage, infiniteQuery.isFetchingNextPage, infiniteQuery.fetchNextPage],
  )

  useEffect(() => {
    return () => {
      observerRef.current?.disconnect()
    }
  }, [])

  const handleMarkAllAsRead = async () => {
    if (unreadCount === 0 || markAllAsReadMutation.isPending) return
    await markAllAsReadMutation.mutateAsync()
  }

  const handleNotificationClick = async (notification: Notification) => {
    setNavigationError(null)
    setBusyNotificationId(notification.id)

    try {
      if (!notification.isRead) {
        await markAsReadMutation.mutateAsync(notification.id)
      }
      const routeTarget = await resolveNotificationTarget(notification)
      invalidateTargetQueries(queryClient, routeTarget)
      onNavigate?.()
      await navigate(routeTarget)
    } catch {
      setNavigationError('Unable to open notification target.')
    } finally {
      setBusyNotificationId(null)
    }
  }

  const handleDelete = useCallback(
    async (notificationId: Guid) => {
      setNavigationError(null)
      setBusyNotificationId(notificationId)

      try {
        await deleteNotificationMutation.mutateAsync(notificationId)
      } finally {
        setBusyNotificationId(null)
      }
    },
    [deleteNotificationMutation],
  )

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Header */}
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        sx={{ px: 2.5, pt: 2, pb: 1 }}
      >
        <Typography variant="h3">Notifications</Typography>
        <Stack direction="row" spacing={0.5} alignItems="center">
          <Button
            size="small"
            onClick={handleMarkAllAsRead}
            disabled={unreadCount === 0 || markAllAsReadMutation.isPending}
            aria-label="Mark all as read"
          >
            Mark all read
          </Button>
          {onNavigate ? (
            <IconButton size="small" onClick={onNavigate} aria-label="Close notifications">
              <CloseIcon fontSize="small" />
            </IconButton>
          ) : null}
        </Stack>
      </Stack>

      {/* Tabs */}
      <Box sx={{ px: 2.5 }}>
        <Tabs
          value={tab}
          onChange={(_, value: TabValue) => setTab(value)}
          sx={{
            minHeight: 36,
            '& .MuiTab-root': { minHeight: 36, py: 0.5, textTransform: 'none', fontWeight: 600 },
          }}
        >
          <Tab
            label={unreadCount > 0 ? `Unread (${unreadCount})` : 'Unread'}
            value="unread"
          />
          <Tab label="All" value="all" />
        </Tabs>
      </Box>

      <Divider />

      {navigationError ? (
        <Alert severity="warning" sx={{ mx: 2, mt: 1 }}>
          {navigationError}
        </Alert>
      ) : null}

      {/* Content */}
      <Box sx={{ flex: 1, overflowY: 'auto' }}>
        {infiniteQuery.isLoading ? (
          <NotificationSkeletons />
        ) : infiniteQuery.isError ? (
          <Alert severity="error" sx={{ m: 2 }}>
            Unable to load notifications.
          </Alert>
        ) : filteredNotifications.length === 0 ? (
          <EmptyState tab={tab} />
        ) : (
          <List sx={{ py: 0.5, px: 1 }}>
            {filteredNotifications.map((notification) => (
              <NotificationItem
                key={notification.id}
                notification={notification}
                isBusy={busyNotificationId === notification.id}
                onClick={() => void handleNotificationClick(notification)}
                onDelete={() => void handleDelete(notification.id)}
              />
            ))}

            {/* Infinite scroll sentinel */}
            <Box ref={handleSentinel} sx={{ height: 1 }} />

            {infiniteQuery.isFetchingNextPage ? (
              <Stack alignItems="center" py={2}>
                <CircularProgress size={20} />
              </Stack>
            ) : null}
          </List>
        )}
      </Box>
    </Box>
  )
}

type NotificationItemProps = {
  notification: Notification
  isBusy: boolean
  onClick: () => void
  onDelete: () => void
}

const SWIPE_COMMIT_PX = 96
const SWIPE_DIRECTION_LOCK_PX = 8
const SWIPE_FLICK_VELOCITY = 0.5 // px per ms
const SWIPE_RUBBER_BAND = 0.35
const SWIPE_COMMIT_MS = 220
const SWIPE_RESET_MS = 180

function NotificationItem({ notification, isBusy, onClick, onDelete }: NotificationItemProps) {
  const creatorName = notification.creator?.userName ?? notification.creator?.email ?? 'Someone'
  const [expanded, setExpanded] = useState(false)
  const theme = useTheme()
  const {
    rootRef,
    swipeHandlers,
    suppressNextClick,
    offsetX,
    direction,
    dragging,
    committing,
    collapsing,
    height,
  } = useSwipeToDismiss({ disabled: isBusy, onDismiss: onDelete })

  const toggleExpand = (event: React.MouseEvent) => {
    event.stopPropagation()
    setExpanded((prev) => !prev)
  }

  const handleClick = (event: React.MouseEvent<HTMLDivElement>) => {
    if (suppressNextClick()) {
      event.preventDefault()
      return
    }
    onClick()
  }

  const handleDeleteClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation()
    onDelete()
  }

  return (
    <Box
      ref={rootRef}
      sx={{
        position: 'relative',
        mb: 0.5,
        borderRadius: 1,
        overflow: 'hidden',
        touchAction: 'pan-y',
        height: height != null ? `${height}px` : 'auto',
        transition: collapsing
          ? `height ${SWIPE_COMMIT_MS}ms ease-out, opacity ${SWIPE_COMMIT_MS}ms ease-out, margin ${SWIPE_COMMIT_MS}ms ease-out`
          : undefined,
        opacity: collapsing ? 0 : 1,
        marginBottom: collapsing ? 0 : undefined,
      }}
    >
      <Box
        aria-hidden
        sx={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: direction === 'right' ? 'flex-start' : 'flex-end',
          px: 2.5,
          bgcolor: 'error.main',
          color: 'error.contrastText',
          opacity: Math.min(1, Math.abs(offsetX) / SWIPE_COMMIT_PX),
          pointerEvents: 'none',
        }}
      >
        <Stack direction="row" alignItems="center" spacing={1}>
          <CloseIcon sx={{ fontSize: 20 }} />
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            Dismiss
          </Typography>
        </Stack>
      </Box>

      <ListItemButton
        onClick={handleClick}
        {...swipeHandlers}
        disabled={isBusy}
        sx={{
          borderRadius: 1,
          px: 1.5,
          py: 1,
          gap: 1.5,
          alignItems: 'flex-start',
          position: 'relative',
          bgcolor: 'transparent',
          transform: `translateX(${offsetX}px)`,
          transition: dragging
            ? 'none'
            : `transform ${committing ? SWIPE_COMMIT_MS : SWIPE_RESET_MS}ms ${theme.transitions.easing.easeOut}`,
          '&:hover .notification-delete, &:focus-within .notification-delete': { opacity: 1 },
          '&:hover .notification-expand, &:focus-within .notification-expand': { opacity: 1 },
        }}
      >
      {/* Unread dot */}
      {!notification.isRead ? (
        <Box
          sx={{
            position: 'absolute',
            left: 4,
            top: '50%',
            transform: 'translateY(-50%)',
            width: 8,
            height: 8,
            borderRadius: '50%',
            bgcolor: 'primary.main',
          }}
          aria-label="Unread"
        />
      ) : null}

      {/* Avatar */}
      <UserAvatar
        userId={notification.createdBy ?? undefined}
        name={creatorName}
        sx={{
          width: 36,
          height: 36,
          fontSize: '0.8rem',
          bgcolor: 'primary.main',
          alignSelf: 'center',
          ml: notification.isRead ? 0 : 1,
        }}
      />

      {/* Content */}
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Stack direction="row" alignItems="center" spacing={0.5}>
          <Typography
            variant="body2"
            sx={{ fontWeight: notification.isRead ? 500 : 700 }}
            noWrap
          >
            {creatorName}
          </Typography>
          <Box sx={{ flex: 1 }} />
          <Typography variant="caption" color="text.secondary" noWrap sx={{ flexShrink: 0 }}>
            {formatRelativeTime(notification.createdAt)}
          </Typography>
        </Stack>

        <Typography
          variant="body2"
          sx={{
            fontWeight: notification.isRead ? 400 : 600,
            wordBreak: 'break-word',
          }}
        >
          {notification.title}
        </Typography>

        <Stack direction="row" alignItems="flex-start" spacing={0.25} sx={{ mt: 0.25 }}>
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{
              flex: 1,
              minWidth: 0,
              wordBreak: 'break-word',
              ...(!expanded && {
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }),
            }}
          >
            {notification.message}
          </Typography>
          <IconButton
            className="notification-expand"
            size="small"
            onClick={toggleExpand}
            aria-label={expanded ? 'Collapse message' : 'Expand message'}
            sx={{
              p: 0,
              ml: 0.25,
              mt: '-2px',
              flexShrink: 0,
              opacity: expanded ? 1 : 0,
              transition: 'opacity 100ms ease',
            }}
          >
            {expanded ? (
              <KeyboardArrowUpIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
            ) : (
              <KeyboardArrowDownIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
            )}
          </IconButton>
        </Stack>

        {/* Type icon badge */}
        <Box sx={{ mt: 0.5 }}>{getNotificationIcon(notification.type)}</Box>
      </Box>

      {/* Delete button (hover reveal) */}
      <IconButton
        className="notification-delete"
        size="small"
        aria-label="Delete notification"
        onClick={handleDeleteClick}
        disabled={isBusy}
        sx={{ opacity: 0, transition: 'opacity 100ms ease', mt: 0.25 }}
      >
        <CloseIcon sx={{ fontSize: 16 }} />
      </IconButton>
      </ListItemButton>
    </Box>
  )
}

type SwipeOptions = {
  disabled: boolean
  onDismiss: () => void
}

type SwipeHandlers = {
  onTouchStart: (event: React.TouchEvent) => void
  onTouchMove: (event: React.TouchEvent) => void
  onTouchEnd: () => void
  onTouchCancel: () => void
}

type SwipeState = {
  rootRef: React.RefCallback<HTMLDivElement>
  swipeHandlers: SwipeHandlers
  suppressNextClick: () => boolean
  offsetX: number
  direction: 'left' | 'right' | null
  dragging: boolean
  committing: boolean
  collapsing: boolean
  height: number | null
}

function useSwipeToDismiss({ disabled, onDismiss }: SwipeOptions): SwipeState {
  const rootElRef = useRef<HTMLDivElement | null>(null)
  const rootRef = useCallback<React.RefCallback<HTMLDivElement>>((node) => {
    rootElRef.current = node
  }, [])
  const startRef = useRef<{ x: number; y: number; t: number; axis: 'h' | 'v' | null } | null>(null)
  const offsetRef = useRef(0)
  const suppressClickRef = useRef(false)
  const [offsetX, setOffsetX] = useState(0)
  const [dragging, setDragging] = useState(false)
  const [committing, setCommitting] = useState(false)
  const [collapsing, setCollapsing] = useState(false)
  const [height, setHeight] = useState<number | null>(null)
  const reducedMotion = usePrefersReducedMotion()

  const direction: 'left' | 'right' | null = offsetX > 0 ? 'right' : offsetX < 0 ? 'left' : null

  const setOffset = useCallback((value: number) => {
    offsetRef.current = value
    setOffsetX(value)
  }, [])

  const commit = useCallback(
    (finalOffset: number) => {
      const width = rootElRef.current?.offsetWidth ?? 320
      const target = finalOffset > 0 ? width : -width
      const measured = rootElRef.current?.offsetHeight ?? null

      suppressClickRef.current = true

      if (reducedMotion) {
        onDismiss()
        return
      }

      setCommitting(true)
      setOffset(target)
      if (measured != null) setHeight(measured)

      // Trigger collapse on the next frame so the height transition has a starting value.
      requestAnimationFrame(() => {
        setCollapsing(true)
        setHeight(0)
      })

      window.setTimeout(() => {
        onDismiss()
      }, SWIPE_COMMIT_MS)
    },
    [onDismiss, reducedMotion, setOffset],
  )

  const reset = useCallback(() => {
    setCommitting(false)
    setOffset(0)
  }, [setOffset])

  const onTouchStart = useCallback(
    (event: React.TouchEvent) => {
      if (disabled || committing || collapsing) return
      const touch = event.touches[0]
      if (!touch) return
      startRef.current = { x: touch.clientX, y: touch.clientY, t: performance.now(), axis: null }
    },
    [disabled, committing, collapsing],
  )

  const onTouchMove = useCallback(
    (event: React.TouchEvent) => {
      if (!startRef.current || disabled) return
      const touch = event.touches[0]
      if (!touch) return

      const dx = touch.clientX - startRef.current.x
      const dy = touch.clientY - startRef.current.y

      if (startRef.current.axis == null) {
        if (Math.abs(dx) < SWIPE_DIRECTION_LOCK_PX && Math.abs(dy) < SWIPE_DIRECTION_LOCK_PX) {
          return
        }
        startRef.current.axis = Math.abs(dx) > Math.abs(dy) ? 'h' : 'v'
        if (startRef.current.axis === 'h') {
          setDragging(true)
        }
      }

      if (startRef.current.axis !== 'h') return

      // Rubber-band past the commit threshold for tactile feel.
      let next = dx
      if (Math.abs(dx) > SWIPE_COMMIT_PX) {
        const excess = Math.abs(dx) - SWIPE_COMMIT_PX
        next = Math.sign(dx) * (SWIPE_COMMIT_PX + excess * SWIPE_RUBBER_BAND)
      }
      setOffset(next)
    },
    [disabled, setOffset],
  )

  const onTouchEnd = useCallback(() => {
    const start = startRef.current
    startRef.current = null

    if (!start || start.axis !== 'h') {
      setDragging(false)
      return
    }

    setDragging(false)

    const finalOffset = offsetRef.current
    const elapsed = Math.max(performance.now() - start.t, 1)
    const velocity = finalOffset / elapsed // px/ms (signed)

    const pastThreshold = Math.abs(finalOffset) >= SWIPE_COMMIT_PX
    const flicked =
      Math.abs(velocity) >= SWIPE_FLICK_VELOCITY && Math.abs(finalOffset) >= SWIPE_COMMIT_PX / 2

    if (pastThreshold || flicked) {
      commit(finalOffset)
    } else {
      reset()
    }
  }, [commit, reset])

  const suppressNextClick = useCallback(() => {
    if (suppressClickRef.current) {
      suppressClickRef.current = false
      return true
    }
    return false
  }, [])

  // Reset suppress flag if user releases without crossing threshold but mid-drag click fires.
  useEffect(() => {
    if (!dragging) return
    suppressClickRef.current = true
  }, [dragging])

  return {
    rootRef,
    swipeHandlers: {
      onTouchStart,
      onTouchMove,
      onTouchEnd,
      onTouchCancel: onTouchEnd,
    },
    suppressNextClick,
    offsetX,
    direction,
    dragging,
    committing,
    collapsing,
    height,
  }
}

const reducedMotionQuery = '(prefers-reduced-motion: reduce)'

function subscribeReducedMotion(onChange: () => void) {
  if (typeof window === 'undefined' || !window.matchMedia) return () => {}
  const mql = window.matchMedia(reducedMotionQuery)
  mql.addEventListener('change', onChange)
  return () => mql.removeEventListener('change', onChange)
}

function getReducedMotion(): boolean {
  if (typeof window === 'undefined' || !window.matchMedia) return false
  return window.matchMedia(reducedMotionQuery).matches
}

function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(
    subscribeReducedMotion,
    getReducedMotion,
    () => false,
  )
}

function EmptyState({ tab }: { tab: TabValue }) {
  return (
    <Stack alignItems="center" justifyContent="center" spacing={1.5} sx={{ py: 8, px: 3 }}>
      <NotificationsOffOutlinedIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
      <Typography variant="h3" color="text.secondary">
        {tab === 'unread' ? 'No unread notifications' : "You're all caught up"}
      </Typography>
      <Typography variant="body2" color="text.secondary" textAlign="center">
        {tab === 'unread'
          ? 'All your notifications have been read.'
          : "When there's activity on your tasks or projects, you'll see it here."}
      </Typography>
    </Stack>
  )
}

function NotificationSkeletons() {
  return (
    <Stack spacing={1} sx={{ px: 2, py: 1.5 }}>
      {Array.from({ length: 5 }, (_, i) => (
        <Stack key={i} direction="row" spacing={1.5} alignItems="flex-start">
          <Skeleton variant="circular" width={36} height={36} />
          <Box sx={{ flex: 1 }}>
            <Skeleton width="60%" height={18} />
            <Skeleton width="90%" height={16} sx={{ mt: 0.5 }} />
            <Skeleton width="40%" height={14} sx={{ mt: 0.5 }} />
          </Box>
        </Stack>
      ))}
    </Stack>
  )
}

function getNotificationIcon(type: NotificationType) {
  const sx = { fontSize: 14, color: 'text.disabled' } as const

  switch (type) {
    case 0:
      return <AssignmentIndOutlinedIcon sx={sx} />
    case 1:
      return <EditOutlinedIcon sx={sx} />
    case 2:
      return <PostAddOutlinedIcon sx={sx} />
    case 3:
      return <DeleteSweepOutlinedIcon sx={sx} />
    case 4:
      return <DriveFileMoveOutlinedIcon sx={sx} />
    case 5:
      return <AlternateEmailIcon sx={sx} />
    default:
      return <NotificationsNoneIcon sx={sx} />
  }
}

function formatRelativeTime(isoDate: string): string {
  const date = new Date(isoDate)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()

  if (diffMs < 0) return 'just now'

  const seconds = Math.floor(diffMs / 1000)
  if (seconds < 60) return 'just now'

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`

  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`

  const weeks = Math.floor(days / 7)
  if (weeks < 4) return `${weeks}w ago`

  // Fallback to date
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

function invalidateTargetQueries(queryClient: QueryClient, target: RouteTarget): void {
  if (target.to === '/projects') {
    void queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    return
  }

  if (target.to === '/projects/$projectId') {
    void queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(target.params.projectId) })
    void queryClient.invalidateQueries({ queryKey: boardsQueryKeys.projectBoards(target.params.projectId) })
    return
  }

  if (target.to === '/projects/$projectId/boards/$boardId') {
    void queryClient.invalidateQueries({ queryKey: projectsQueryKeys.project(target.params.projectId) })
    void queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(target.params.boardId) })
    const cardId = target.search?.cardId
    if (cardId) {
      void queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(cardId) })
    }
  }
}

async function resolveNotificationTarget(notification: Notification): Promise<RouteTarget> {
  const entityType = notification.entityType?.toLowerCase()
  const entityId = notification.entityId

  if (!entityId) {
    return { to: '/projects' }
  }

  if (entityType === 'project') {
    return {
      to: '/projects/$projectId',
      params: { projectId: entityId },
    }
  }

  if (entityType === 'board') {
    try {
      const board = await getBoard(entityId)
      return {
        to: '/projects/$projectId/boards/$boardId',
        params: { projectId: board.projectId, boardId: board.id },
      }
    } catch {
      return { to: '/projects' }
    }
  }

  if (entityType === 'card') {
    try {
      const card = await getCard(entityId)
      const boardId = card.column?.boardId ?? card.column?.board?.id
      const projectId = card.column?.board?.projectId

      if (!boardId || !projectId) {
        return { to: '/projects' }
      }

      return {
        to: '/projects/$projectId/boards/$boardId',
        params: { projectId, boardId },
        search: { cardId: card.id },
      }
    } catch {
      return { to: '/projects' }
    }
  }

  return { to: '/projects' }
}
