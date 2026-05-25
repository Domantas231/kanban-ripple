import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { boardsQueryKeys } from '@/features/boards/api/query-keys'
import { projectsQueryKeys } from '@/features/projects'
import { cardsQueryKeys } from '@/features/cards'
import { favoritesQueryKeys } from '@/features/favorites'
import type { Board, Card, Column, Guid, PaginatedResponse } from '@/lib/types'

export type CreateBoardRequest = {
  name: string
}

export type UpdateBoardRequest = {
  name: string
  position: number
}

export type CreateColumnRequest = {
  name: string
}

export type UpdateColumnRequest = {
  name: string
}

export type ReorderColumnRequest = {
  beforeColumnId?: Guid | null
  afterColumnId?: Guid | null
}

export async function getBoards(projectId: Guid): Promise<Board[]> {
  const response = await apiClient.get<Board[]>(`/api/projects/${projectId}/boards`)
  return response.data
}

export async function getArchivedBoards(): Promise<Board[]> {
  const response = await apiClient.get<Board[]>('/api/boards/archived')
  return response.data
}

export async function getBoard(id: Guid): Promise<Board> {
  const response = await apiClient.get<Board>(`/api/boards/${id}`)
  return response.data
}

export async function createBoard(projectId: Guid, request: CreateBoardRequest): Promise<Board> {
  const response = await apiClient.post<Board>(`/api/projects/${projectId}/boards`, request)
  return response.data
}

export async function importTrelloBoard(projectId: Guid, trelloData: unknown): Promise<Board> {
  const response = await apiClient.post<Board>(`/api/projects/${projectId}/boards/import-trello`, trelloData)
  return response.data
}

export async function updateBoard(id: Guid, request: UpdateBoardRequest): Promise<Board> {
  const response = await apiClient.put<Board>(`/api/boards/${id}`, request)
  return response.data
}

export async function archiveBoard(id: Guid): Promise<void> {
  await apiClient.delete(`/api/boards/${id}`)
}

export async function restoreBoard(id: Guid): Promise<void> {
  await apiClient.post(`/api/boards/${id}/restore`)
}

export async function purgeBoard(id: Guid): Promise<void> {
  await apiClient.delete(`/api/boards/${id}/permanent`)
}

export async function getColumns(boardId: Guid): Promise<Column[]> {
  const response = await apiClient.get<Column[]>(`/api/boards/${boardId}/columns`)
  return response.data
}

export async function createColumn(boardId: Guid, request: CreateColumnRequest): Promise<Column> {
  const response = await apiClient.post<Column>(`/api/boards/${boardId}/columns`, request)
  return response.data
}

export async function updateColumn(id: Guid, request: UpdateColumnRequest): Promise<Column> {
  const response = await apiClient.put<Column>(`/api/columns/${id}`, request)
  return response.data
}

export async function reorderColumns(id: Guid, request: ReorderColumnRequest): Promise<Column> {
  const response = await apiClient.put<Column>(`/api/columns/${id}/reorder`, request)
  return response.data
}

export async function archiveColumn(id: Guid): Promise<void> {
  await apiClient.delete(`/api/columns/${id}`)
}

export async function restoreColumn(id: Guid): Promise<void> {
  await apiClient.post(`/api/columns/${id}/restore`)
}

export async function purgeColumn(id: Guid): Promise<void> {
  await apiClient.delete(`/api/columns/${id}/permanent`)
}

export async function getArchivedColumnsByBoard(boardId: Guid): Promise<Column[]> {
  const response = await apiClient.get<Column[]>(`/api/boards/${boardId}/columns/archived`)
  return response.data
}

export function useBoards(projectId: Guid | undefined) {
  return useQuery({
    queryKey: projectId ? boardsQueryKeys.projectBoards(projectId) : [...boardsQueryKeys.boards, 'project'],
    queryFn: () => getBoards(projectId as Guid),
    enabled: Boolean(projectId),
  })
}

export function useArchivedBoards() {
  return useQuery({
    queryKey: boardsQueryKeys.archivedBoards,
    queryFn: getArchivedBoards,
  })
}

export function useBoard(id: Guid | undefined) {
  return useQuery({
    queryKey: id ? boardsQueryKeys.board(id) : [...boardsQueryKeys.boards, 'detail'],
    queryFn: () => getBoard(id as Guid),
    enabled: Boolean(id),
  })
}

export function useCreateBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ projectId, data }: { projectId: Guid; data: CreateBoardRequest }) =>
      createBoard(projectId, data),
    onSuccess: (board) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.projectBoards(board.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(board.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useImportTrelloBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ projectId, trelloData }: { projectId: Guid; trelloData: unknown }) =>
      importTrelloBoard(projectId, trelloData),
    onSuccess: (board) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.projectBoards(board.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(board.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.boardTags(board.id) })
    },
  })
}

export function useUpdateBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; data: UpdateBoardRequest }) => updateBoard(id, data),
    onSuccess: (board) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(board.id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.projectBoards(board.projectId) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(board.projectId) })
    },
  })
}

export function useArchiveBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => archiveBoard(id),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: boardsQueryKeys.board(id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

export function useRestoreBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => restoreBoard(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function usePurgeBoard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => purgeBoard(id),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: boardsQueryKeys.board(id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards, refetchType: 'all' })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

export function usePurgeColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => purgeColumn(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards, refetchType: 'all' })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useColumns(boardId: Guid | undefined) {
  return useQuery({
    queryKey: boardId ? boardsQueryKeys.boardColumns(boardId) : [...boardsQueryKeys.boards, 'columns'],
    queryFn: () => getColumns(boardId as Guid),
    enabled: Boolean(boardId),
  })
}

export function useCreateColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ boardId, data }: { boardId: Guid; data: CreateColumnRequest }) =>
      createColumn(boardId, data),
    onSuccess: (column) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardColumns(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUpdateColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; data: UpdateColumnRequest }) => updateColumn(id, data),
    onSuccess: (column) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardColumns(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useReorderColumns() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; data: ReorderColumnRequest }) => reorderColumns(id, data),
    onSuccess: (column) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardColumns(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.board(column.boardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useArchivedColumnsByBoard(boardId: Guid | undefined, enabled = false) {
  return useQuery({
    queryKey: boardId ? boardsQueryKeys.boardArchivedColumns(boardId) : [...boardsQueryKeys.boards, 'columns', 'archived'],
    queryFn: () => getArchivedColumnsByBoard(boardId as Guid),
    enabled: Boolean(boardId) && enabled,
  })
}

export function useArchiveColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => archiveColumn(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards, refetchType: 'all' })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useRestoreColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => restoreColumn(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: cardsQueryKeys.archivedCards })

      const previousArchivedCardQueries = queryClient.getQueriesData<PaginatedResponse<Card>>({
        queryKey: cardsQueryKeys.archivedCards,
      })

      queryClient.setQueriesData<PaginatedResponse<Card>>(
        { queryKey: cardsQueryKeys.archivedCards },
        (previous) => {
          if (!previous) {
            return previous
          }

          const updatedItems = previous.items.filter((card) => card.columnId !== id)

          return {
            ...previous,
            items: updatedItems,
            totalCount: Math.max(0, previous.totalCount - (previous.items.length - updatedItems.length)),
          }
        },
      )

      return { previousArchivedCardQueries }
    },
    onError: (_, __, context) => {
      context?.previousArchivedCardQueries.forEach(([queryKey, data]) => {
        queryClient.setQueryData(queryKey, data)
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}
