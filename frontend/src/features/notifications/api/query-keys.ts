export const notificationsQueryKeys = {
  notifications: ['notifications'] as const,
  notificationsPage: (page: number, pageSize: number) =>
    ['notifications', 'list', page, pageSize] as const,
  notificationsUnreadCount: ['notifications', 'unreadCount'] as const,
} as const
