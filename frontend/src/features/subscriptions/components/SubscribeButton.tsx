import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive'
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone'
import { IconButton, Tooltip, type IconButtonProps } from '@mui/material'
import {
  useCardSubscriptions,
  useBoardSubscriptions,
  useColumnSubscriptions,
  useProjectSubscriptions,
  useSubscribe,
  useUnsubscribeByEntity,
} from '@/features/subscriptions/api/subscriptions'
import { useAuthStore } from '@/features/auth'
import type { EntityType, Guid } from '@/lib/types'

const ENTITY_TYPE_CARD: EntityType = 0
const ENTITY_TYPE_COLUMN: EntityType = 1
const ENTITY_TYPE_PROJECT: EntityType = 2
const ENTITY_TYPE_BOARD: EntityType = 3

type SubscribeButtonProps = {
  entityType: EntityType
  entityId: Guid
  disabled?: boolean
  iconButtonProps?: Omit<IconButtonProps, 'onClick' | 'disabled' | 'children' | 'aria-label'>
}

export function SubscribeButton({
  entityType,
  entityId,
  disabled = false,
  iconButtonProps,
}: SubscribeButtonProps) {
  const currentUserId = useAuthStore((state) => state.user?.id)

  const cardSubscriptionsQuery = useCardSubscriptions(entityType === ENTITY_TYPE_CARD ? entityId : undefined)
  const columnSubscriptionsQuery = useColumnSubscriptions(entityType === ENTITY_TYPE_COLUMN ? entityId : undefined)
  const boardSubscriptionsQuery = useBoardSubscriptions(entityType === ENTITY_TYPE_BOARD ? entityId : undefined)
  const projectSubscriptionsQuery = useProjectSubscriptions(entityType === ENTITY_TYPE_PROJECT ? entityId : undefined)

  const subscribeMutation = useSubscribe()
  const unsubscribeMutation = useUnsubscribeByEntity()

  const activeQuery =
    entityType === ENTITY_TYPE_CARD
      ? cardSubscriptionsQuery
      : entityType === ENTITY_TYPE_COLUMN
        ? columnSubscriptionsQuery
        : entityType === ENTITY_TYPE_BOARD
          ? boardSubscriptionsQuery
          : projectSubscriptionsQuery

  const isSubscribed = Boolean(currentUserId && (activeQuery.data ?? []).includes(currentUserId))

  const isBusy =
    disabled ||
    activeQuery.isLoading ||
    subscribeMutation.isPending ||
    unsubscribeMutation.isPending ||
    !currentUserId

  const handleToggle = async () => {
    if (isBusy) {
      return
    }

    if (isSubscribed) {
      await unsubscribeMutation.mutateAsync({
        entityType,
        entityId,
      })
      return
    }

    await subscribeMutation.mutateAsync({
      entityType,
      entityId,
    })
  }

  return (
    <Tooltip title={isSubscribed ? 'Unsubscribe' : 'Subscribe'}>
      <span>
        <IconButton
          onClick={() => void handleToggle()}
          disabled={isBusy}
          size={iconButtonProps?.size ?? 'small'}
          aria-label={isSubscribed ? 'Unsubscribe' : 'Subscribe'}
          {...iconButtonProps}
        >
          {isSubscribed ? <NotificationsActiveIcon fontSize="small" /> : <NotificationsNoneIcon fontSize="small" />}
        </IconButton>
      </span>
    </Tooltip>
  )
}
