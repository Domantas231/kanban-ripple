import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { notificationsQueryKeys } from '@/features/notifications/api/query-keys'
import type { Guid, Notification, PaginatedResponse } from '@/lib/types'

type PagedParams = {
  page?: number
  pageSize?: number
}

const DEFAULT_NOTIFICATION_PAGE_SIZE = 20
const DEFAULT_UNREAD_COUNT_PAGE_SIZE = 100
const UNREAD_COUNT_POLL_INTERVAL_MS = 30_000

export async function getNotifications({
  page = 1,
  pageSize = DEFAULT_NOTIFICATION_PAGE_SIZE,
}: PagedParams): Promise<PaginatedResponse<Notification>> {
  const response = await apiClient.get<PaginatedResponse<Notification>>('/api/notifications', {
    params: {
      page,
      pageSize,
    },
  })

  return response.data
}

export async function markAsRead(id: Guid): Promise<void> {
  await apiClient.put(`/api/notifications/${id}/read`)
}

export async function markAllAsRead(): Promise<void> {
  await apiClient.put('/api/notifications/read-all')
}

export async function deleteNotification(id: Guid): Promise<void> {
  await apiClient.delete(`/api/notifications/${id}`)
}

export async function getUnreadCount(): Promise<number> {
  let unreadCount = 0
  let page = 1

  while (true) {
    const response = await getNotifications({
      page,
      pageSize: DEFAULT_UNREAD_COUNT_PAGE_SIZE,
    })

    unreadCount += response.items.reduce(
      (count, notification) => count + (notification.isRead ? 0 : 1),
      0,
    )

    const totalPages = Math.max(1, Math.ceil(response.totalCount / response.pageSize))
    if (page >= totalPages) {
      break
    }

    page += 1
  }

  return unreadCount
}

export function useNotifications(page = 1, pageSize = DEFAULT_NOTIFICATION_PAGE_SIZE) {
  return useQuery({
    queryKey: notificationsQueryKeys.notificationsPage(page, pageSize),
    queryFn: () => getNotifications({ page, pageSize }),
  })
}

export function useInfiniteNotifications(pageSize = DEFAULT_NOTIFICATION_PAGE_SIZE) {
  return useInfiniteQuery({
    queryKey: [...notificationsQueryKeys.notifications, 'infinite', pageSize],
    queryFn: ({ pageParam }) => getNotifications({ page: pageParam, pageSize }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => {
      const totalPages = Math.max(1, Math.ceil(lastPage.totalCount / lastPage.pageSize))
      return lastPage.page < totalPages ? lastPage.page + 1 : undefined
    },
  })
}

export function useUnreadCount() {
  return useQuery({
    queryKey: notificationsQueryKeys.notificationsUnreadCount,
    queryFn: getUnreadCount,
    refetchInterval: UNREAD_COUNT_POLL_INTERVAL_MS,
  })
}

export function useMarkAsRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => markAsRead(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notifications })
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notificationsUnreadCount })
    },
  })
}

export function useMarkAllAsRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: markAllAsRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notifications })
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notificationsUnreadCount })
    },
  })
}

export function useDeleteNotification() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => deleteNotification(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notifications })
      queryClient.invalidateQueries({ queryKey: notificationsQueryKeys.notificationsUnreadCount })
    },
  })
}
