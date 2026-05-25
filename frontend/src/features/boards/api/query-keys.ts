export const boardsQueryKeys = {
  boards: ['boards'] as const,
  archivedBoards: ['boards', 'archived'] as const,
  projectBoards: (projectId: string) => ['boards', 'project', projectId] as const,
  board: (boardId: string) => ['boards', boardId] as const,
  boardColumns: (boardId: string) => ['boards', boardId, 'columns'] as const,
  boardCards: (boardId: string) => ['boards', boardId, 'cards'] as const,
  boardArchivedColumns: (boardId: string) => ['boards', boardId, 'columns', 'archived'] as const,
  boardArchivedCards: (boardId: string) => ['boards', boardId, 'cards', 'archived'] as const,
} as const
