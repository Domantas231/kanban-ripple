export const searchQueryKeys = {
  globalSearch: (query: string) => ['search', 'global', query] as const,
  searchCards: (projectId: string, query: string, page: number, pageSize: number) =>
    ['projects', projectId, 'cards', 'search', query, page, pageSize] as const,
  filteredBoardCards: (
    boardId: string,
    tagIds?: string[],
    userIds?: string[],
  ) => ['boards', boardId, 'cards', 'filter', tagIds ?? [], userIds ?? []] as const,
} as const
