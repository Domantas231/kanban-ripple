import { useNavigate } from '@tanstack/react-router'
import { useQueryClient } from '@tanstack/react-query'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import { alpha } from '@mui/material/styles'
import AssignmentOutlinedIcon from '@mui/icons-material/AssignmentOutlined'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import NotificationsActiveOutlinedIcon from '@mui/icons-material/NotificationsActiveOutlined'
import NotificationsOffOutlinedIcon from '@mui/icons-material/NotificationsOffOutlined'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import { useMySubscriptions, useUnsubscribe } from '@/features/subscriptions/api/subscriptions'
import { useUiStore } from '@/stores/uiStore'
import { subscriptionsQueryKeys } from '@/features/subscriptions/api/query-keys'
import type { EntityType, MySubscriptionDto } from '@/lib/types'

const ENTITY_TYPE_ICONS: Record<EntityType, typeof AssignmentOutlinedIcon> = {
  0: AssignmentOutlinedIcon,
  1: ViewColumnOutlinedIcon,
  2: DashboardOutlinedIcon,
  3: ViewKanbanOutlinedIcon,
}

const ENTITY_TYPE_COLORS: Record<EntityType, string> = {
  0: 'primary.main',
  1: 'info.main',
  2: 'success.main',
  3: 'warning.main',
}

export function SubscriptionsSection() {
  const { data: subscriptions, isLoading } = useMySubscriptions()
  const unsubscribeMutation = useUnsubscribe()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const enqueueToast = useUiStore((s) => s.enqueueToast)

  async function handleUnsubscribe(e: React.MouseEvent, id: string) {
    e.stopPropagation()
    try {
      await unsubscribeMutation.mutateAsync(id)
      await queryClient.invalidateQueries({ queryKey: subscriptionsQueryKeys.mySubscriptions })
      enqueueToast({ message: 'Unsubscribed successfully.', severity: 'success' })
    } catch {
      enqueueToast({ message: 'Failed to unsubscribe.', severity: 'error' })
    }
  }

  function handleNavigate(sub: MySubscriptionDto) {
    if (!sub.projectId) return
    switch (sub.entityType) {
      case 0:
        if (sub.boardId) {
          void navigate({
            to: '/projects/$projectId/boards/$boardId',
            params: { projectId: sub.projectId, boardId: sub.boardId },
            search: { cardId: sub.entityId },
          })
        }
        break
      case 1:
        if (sub.boardId) {
          void navigate({
            to: '/projects/$projectId/boards/$boardId',
            params: { projectId: sub.projectId, boardId: sub.boardId },
          })
        }
        break
      case 3:
        void navigate({
          to: '/projects/$projectId/boards/$boardId',
          params: { projectId: sub.projectId, boardId: sub.entityId },
        })
        break
      case 2:
        void navigate({
          to: '/projects/$projectId',
          params: { projectId: sub.entityId },
        })
        break
    }
  }

  return (
    <Card variant="outlined">
      <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
        <Stack spacing={2.5}>
          <Box>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
              <NotificationsActiveOutlinedIcon sx={{ fontSize: 20, color: 'primary.main' }} />
              <Typography variant="h6" sx={{ fontWeight: 700 }}>
                Subscriptions
              </Typography>
            </Stack>
            <Typography variant="body2" color="text.secondary">
              You receive notifications when subscribed items are updated. Unsubscribe to stop receiving them.
            </Typography>
          </Box>

          {isLoading && (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
              <CircularProgress size={28} />
            </Box>
          )}

          {!isLoading && (!subscriptions || subscriptions.length === 0) && (
            <Box sx={{ py: 2, textAlign: 'center' }}>
              <NotificationsOffOutlinedIcon sx={{ fontSize: 40, color: 'text.disabled', mb: 1 }} />
              <Typography variant="body2" color="text.secondary">
                You have no active subscriptions.
              </Typography>
            </Box>
          )}

          {!isLoading && subscriptions && subscriptions.length > 0 && (
            <Stack spacing={1}>
              {subscriptions.map((sub) => {
                const EntityIcon = ENTITY_TYPE_ICONS[sub.entityType]
                const iconColor = ENTITY_TYPE_COLORS[sub.entityType]
                const isClickable = Boolean(sub.projectId)

                return (
                  <Box
                    key={sub.id}
                    onClick={isClickable ? () => handleNavigate(sub) : undefined}
                    sx={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: { xs: 1, sm: 2 },
                      px: { xs: 1.25, sm: 2 },
                      py: 1.5,
                      borderRadius: 2,
                      bgcolor: (theme) =>
                        theme.palette.mode === 'dark'
                          ? 'rgba(255,255,255,0.03)'
                          : 'rgba(220,230,245,0.35)',
                      border: '1px solid',
                      borderColor: 'divider',
                      ...(isClickable && {
                        cursor: 'pointer',
                        '&:hover': { bgcolor: 'action.hover' },
                      }),
                    }}
                  >
                    <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0, flex: 1 }}>
                      <EntityIcon sx={{ fontSize: 20, color: iconColor, flexShrink: 0 }} />
                      <Box sx={{ minWidth: 0, flex: 1 }}>
                        <Typography
                          variant="body2"
                          sx={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                        >
                          {sub.entityName}
                        </Typography>
                        {(sub.boardName || sub.projectName || sub.columnName) && (
                          <Stack
                            direction="row"
                            spacing={0.5}
                            alignItems="center"
                            useFlexGap
                            flexWrap="wrap"
                            sx={{ mt: 0.25 }}
                          >
                            {sub.projectName && sub.entityType !== 2 && (
                              <Tooltip title={`Workspace: ${sub.projectName}`}>
                                <Chip
                                  icon={<DashboardOutlinedIcon sx={{ fontSize: 12, ml: 0.5 }} />}
                                  label={sub.projectName}
                                  size="small"
                                  aria-label={`Workspace: ${sub.projectName}`}
                                  sx={{
                                    height: 18,
                                    fontSize: 10,
                                    fontWeight: 600,
                                    bgcolor: (theme) => alpha(theme.palette.success.main, 0.12),
                                    color: 'success.main',
                                    '& .MuiChip-icon': { color: 'success.main', ml: 0.25, mr: -0.25 },
                                    '& .MuiChip-label': { px: 0.5 },
                                  }}
                                />
                              </Tooltip>
                            )}
                            {sub.projectName && sub.entityType !== 2 && sub.boardName && sub.entityType !== 3 && (
                              <Typography variant="caption" sx={{ color: 'text.disabled', fontSize: 11, lineHeight: 1 }}>
                                /
                              </Typography>
                            )}
                            {sub.boardName && sub.entityType !== 3 && (
                              <Tooltip title={`Board: ${sub.boardName}`}>
                                <Chip
                                  icon={<ViewKanbanOutlinedIcon sx={{ fontSize: 12, ml: 0.5 }} />}
                                  label={sub.boardName}
                                  size="small"
                                  aria-label={`Board: ${sub.boardName}`}
                                  sx={{
                                    height: 18,
                                    fontSize: 10,
                                    fontWeight: 600,
                                    bgcolor: (theme) => alpha(theme.palette.warning.main, 0.12),
                                    color: 'warning.main',
                                    '& .MuiChip-icon': { color: 'warning.main', ml: 0.25, mr: -0.25 },
                                    '& .MuiChip-label': { px: 0.5 },
                                  }}
                                />
                              </Tooltip>
                            )}
                            {sub.columnName && sub.entityType === 0 && (
                              <>
                                <Typography variant="caption" sx={{ color: 'text.disabled', fontSize: 11, lineHeight: 1 }}>
                                  /
                                </Typography>
                                <Tooltip title={`List: ${sub.columnName}`}>
                                  <Chip
                                    icon={<ViewColumnOutlinedIcon sx={{ fontSize: 12, ml: 0.5 }} />}
                                    label={sub.columnName}
                                    size="small"
                                    aria-label={`List: ${sub.columnName}`}
                                    sx={{
                                      height: 18,
                                      fontSize: 10,
                                      fontWeight: 600,
                                      bgcolor: (theme) => alpha(theme.palette.info.main, 0.12),
                                      color: 'info.main',
                                      '& .MuiChip-icon': { color: 'info.main', ml: 0.25, mr: -0.25 },
                                      '& .MuiChip-label': { px: 0.5 },
                                    }}
                                  />
                                </Tooltip>
                              </>
                            )}
                          </Stack>
                        )}
                      </Box>
                    </Stack>
                    <Tooltip title="Unsubscribe">
                      <IconButton
                        size="small"
                        aria-label={`Unsubscribe from ${sub.entityName}`}
                        onClick={(e) => handleUnsubscribe(e, sub.id)}
                        disabled={unsubscribeMutation.isPending}
                        sx={{ color: 'text.secondary', '&:hover': { color: 'error.main' } }}
                      >
                        <NotificationsOffOutlinedIcon sx={{ fontSize: 18 }} />
                      </IconButton>
                    </Tooltip>
                  </Box>
                )
              })}
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}
