export const cardsQueryKeys = {
  archivedCards: ['cards', 'archived'] as const,
  card: (cardId: string) => ['cards', cardId] as const,
  cardActivities: (cardId: string) => ['cards', cardId, 'activities'] as const,
  cardComments: (cardId: string) => ['cards', cardId, 'comments'] as const,
  attachmentUrl: (attachmentId: string) => ['attachments', attachmentId, 'url'] as const,
  cardGoogleDriveLinks: (cardId: string) => ['cards', cardId, 'googleDriveLinks'] as const,
  boardTags: (boardId: string) => ['boards', boardId, 'tags'] as const,
} as const
