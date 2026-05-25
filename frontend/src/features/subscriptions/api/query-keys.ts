export const subscriptionsQueryKeys = {
  subscriptions: ['subscriptions'] as const,
  mySubscriptions: ['subscriptions', 'mine'] as const,
  cardSubscriptions: (cardId: string) => ['subscriptions', 'card', cardId] as const,
  columnSubscriptions: (columnId: string) => ['subscriptions', 'column', columnId] as const,
  boardSubscriptions: (boardId: string) => ['subscriptions', 'board', boardId] as const,
  projectSubscriptions: (projectId: string) => ['subscriptions', 'project', projectId] as const,
} as const
