import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { cardsQueryKeys } from '@/features/cards/api/query-keys'
import { boardsQueryKeys } from '@/features/boards'
import { projectsQueryKeys } from '@/features/projects'
import { favoritesQueryKeys } from '@/features/favorites'
import type {
  Attachment,
  Card,
  CardActivity,
  CreateCardData,
  Guid,
  MoveCardData,
  PaginatedResponse,
  ScheduleCardData,
  Subtask,
  SwimlaneView,
  UpdateCardData,
} from '@/lib/types'

type PagedParams = {
  page?: number
  pageSize?: number
}

export type CreateSubtaskRequest = {
  description: string
  completed?: boolean
}

export type UpdateSubtaskRequest = {
  description?: string
  completed?: boolean
  position?: number
}

export type AttachmentUrlResponse = {
  url: string
}

type MoveCardVariables = {
  id: Guid
  boardId: Guid
  data: MoveCardData
  optimisticPosition?: number
}

type MoveCardContext = {
  previousBoardCardQueries: Array<[readonly unknown[], PaginatedResponse<Card> | undefined]>
}

export async function getCards({ boardId, page = 1, pageSize = 50 }: PagedParams & { boardId: Guid }): Promise<PaginatedResponse<Card>> {
  const response = await apiClient.get<PaginatedResponse<Card>>(`/api/boards/${boardId}/cards`, {
    params: {
      page,
      pageSize,
    },
  })

  return response.data
}

export async function getCard(id: Guid): Promise<Card> {
  const response = await apiClient.get<Card>(`/api/cards/${id}`)
  return response.data
}

export async function getCardActivities(cardId: Guid): Promise<CardActivity[]> {
  const response = await apiClient.get<CardActivity[]>(`/api/cards/${cardId}/activities`)
  return response.data
}

export async function createCard(columnId: Guid, request: CreateCardData): Promise<Card> {
  const response = await apiClient.post<Card>(`/api/columns/${columnId}/cards`, request)
  return response.data
}

export async function updateCard(id: Guid, request: UpdateCardData): Promise<Card> {
  const response = await apiClient.put<Card>(`/api/cards/${id}`, request)
  return response.data
}

export async function archiveCard(id: Guid): Promise<void> {
  await apiClient.delete(`/api/cards/${id}`)
}

export async function scheduleCard(id: Guid, request: ScheduleCardData): Promise<Card> {
  const response = await apiClient.put<Card>(`/api/cards/${id}/schedule`, request)
  return response.data
}

export async function moveCard(id: Guid, request: MoveCardData): Promise<Card> {
  const response = await apiClient.put<Card>(`/api/cards/${id}/move`, request)
  return response.data
}

export async function assignTag(cardId: Guid, tagId: Guid): Promise<void> {
  await apiClient.post(`/api/cards/${cardId}/tags/${tagId}`)
}

export async function unassignTag(cardId: Guid, tagId: Guid): Promise<void> {
  await apiClient.delete(`/api/cards/${cardId}/tags/${tagId}`)
}

export async function assignUser(cardId: Guid, userId: Guid): Promise<void> {
  await apiClient.post(`/api/cards/${cardId}/assignees/${userId}`)
}

export async function unassignUser(cardId: Guid, userId: Guid): Promise<void> {
  await apiClient.delete(`/api/cards/${cardId}/assignees/${userId}`)
}

export async function addAttachment(cardId: Guid, file: File): Promise<Attachment> {
  const formData = new FormData()
  formData.append('file', file)

  const response = await apiClient.post<Attachment>(`/api/cards/${cardId}/attachments`, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  })

  return response.data
}

export async function removeAttachment(id: Guid): Promise<void> {
  await apiClient.delete(`/api/attachments/${id}`)
}

export async function getAttachmentUrl(id: Guid): Promise<AttachmentUrlResponse> {
  const response = await apiClient.get<AttachmentUrlResponse>(`/api/attachments/${id}`)
  return response.data
}

export async function downloadAttachment(id: Guid, filename: string): Promise<void> {
  const response = await apiClient.get(`/api/attachments/${id}/download`, {
    responseType: 'blob',
  })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

export async function createSubtask(cardId: Guid, request: CreateSubtaskRequest): Promise<Subtask> {
  const response = await apiClient.post<Subtask>(`/api/cards/${cardId}/subtasks`, request)
  return response.data
}

export async function updateSubtask(id: Guid, request: UpdateSubtaskRequest): Promise<Subtask> {
  const response = await apiClient.put<Subtask>(`/api/subtasks/${id}`, request)
  return response.data
}

export async function deleteSubtask(id: Guid): Promise<void> {
  await apiClient.delete(`/api/subtasks/${id}`)
}

export async function getArchivedCards({ page = 1, pageSize = 25 }: PagedParams): Promise<PaginatedResponse<Card>> {
  const response = await apiClient.get<PaginatedResponse<Card>>('/api/cards/archived', {
    params: {
      page,
      pageSize,
    },
  })

  return response.data
}

export async function restoreCard(id: Guid): Promise<void> {
  await apiClient.post(`/api/cards/${id}/restore`)
}

export async function purgeCard(id: Guid): Promise<void> {
  await apiClient.delete(`/api/cards/${id}/permanent`)
}

export async function getArchivedCardsByBoard(
  boardId: Guid,
  { page = 1, pageSize = 25 }: PagedParams = {},
): Promise<PaginatedResponse<Card>> {
  const response = await apiClient.get<PaginatedResponse<Card>>(`/api/boards/${boardId}/cards/archived`, {
    params: { page, pageSize },
  })
  return response.data
}

export function useCards(boardId: Guid | undefined, page = 1, pageSize = 50) {
  return useQuery({
    queryKey: boardId ? [...boardsQueryKeys.boardCards(boardId), page, pageSize] : [...boardsQueryKeys.boards, 'cards'],
    queryFn: () => getCards({ boardId: boardId as Guid, page, pageSize }),
    enabled: Boolean(boardId),
  })
}

export function useCard(id: Guid | undefined) {
  return useQuery({
    queryKey: id ? cardsQueryKeys.card(id) : ['cards', 'detail'],
    queryFn: () => getCard(id as Guid),
    enabled: Boolean(id),
  })
}

export function useCardActivities(cardId: Guid | undefined) {
  return useQuery({
    queryKey: cardId ? cardsQueryKeys.cardActivities(cardId) : ['cards', 'activities'],
    queryFn: () => getCardActivities(cardId as Guid),
    enabled: Boolean(cardId),
  })
}

export function useCreateCard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ columnId, data }: { columnId: Guid; data: CreateCardData }) => createCard(columnId, data),
    onSuccess: (card) => {
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(card.id) })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUpdateCard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; data: UpdateCardData }) => updateCard(id, data),
    onSuccess: (card) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(card.id) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(card.id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useArchiveCard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => archiveCard(id),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: cardsQueryKeys.card(id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards, refetchType: 'all' })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

export function useArchivedCardsByBoard(boardId: Guid | undefined, enabled = false, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: boardId
      ? [...boardsQueryKeys.boardArchivedCards(boardId), page, pageSize]
      : [...boardsQueryKeys.boards, 'cards', 'archived'],
    queryFn: () => getArchivedCardsByBoard(boardId as Guid, { page, pageSize }),
    enabled: Boolean(boardId) && enabled,
  })
}

export function useArchivedCards(page = 1, pageSize = 25) {
  return useQuery({
    queryKey: [...cardsQueryKeys.archivedCards, page, pageSize],
    queryFn: () => getArchivedCards({ page, pageSize }),
  })
}

export function useRestoreCard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => restoreCard(id),
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

          const updatedItems = previous.items.filter((card) => card.id !== id)

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
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(id) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function usePurgeCard() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => purgeCard(id),
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

          const updatedItems = previous.items.filter((card) => card.id !== id)

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
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: cardsQueryKeys.card(id) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.archivedCards })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
    },
  })
}

type ScheduleCardVariables = {
  id: Guid
  data: ScheduleCardData
  projectId?: Guid
}

type ScheduleCardContext = {
  previousSwimlane: SwimlaneView | undefined
  projectId: Guid | undefined
}

export function useScheduleCard() {
  const queryClient = useQueryClient()

  return useMutation<Card, Error, ScheduleCardVariables, ScheduleCardContext>({
    mutationFn: ({ id, data }) => scheduleCard(id, data),
    onMutate: async ({ id, data, projectId }) => {
      if (!projectId) {
        return { previousSwimlane: undefined, projectId: undefined }
      }

      await queryClient.cancelQueries({ queryKey: projectsQueryKeys.projectSwimlane(projectId) })

      const previousSwimlane = queryClient.getQueryData<SwimlaneView>(
        projectsQueryKeys.projectSwimlane(projectId),
      )

      queryClient.setQueryData<SwimlaneView>(
        projectsQueryKeys.projectSwimlane(projectId),
        (previous) => {
          if (!previous) return previous

          return {
            ...previous,
            boards: previous.boards.map((board) => ({
              ...board,
              columns: board.columns.map((column) => ({
                ...column,
                cards: column.cards.map((card) =>
                  card.id === id
                    ? { ...card, startDate: data.startDate, dueDate: data.dueDate }
                    : card,
                ),
              })),
            })),
          }
        },
      )

      return { previousSwimlane, projectId }
    },
    onError: (_, __, context) => {
      if (!context?.projectId || context.previousSwimlane === undefined) return

      queryClient.setQueryData(
        projectsQueryKeys.projectSwimlane(context.projectId),
        context.previousSwimlane,
      )
    },
    onSettled: (card, _, variables) => {
      if (card) {
        queryClient.setQueryData(cardsQueryKeys.card(card.id), card)
      }
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.id) })
      if (variables.projectId) {
        queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projectSwimlane(variables.projectId) })
      }
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useMoveCard() {
  const queryClient = useQueryClient()

  return useMutation<Card, Error, MoveCardVariables, MoveCardContext>({
    mutationFn: ({ id, data }) => moveCard(id, data),
    onMutate: async ({ id, boardId, data, optimisticPosition }) => {
      await queryClient.cancelQueries({ queryKey: boardsQueryKeys.boardCards(boardId) })

      const previousBoardCardQueries = queryClient.getQueriesData<PaginatedResponse<Card>>({
        queryKey: boardsQueryKeys.boardCards(boardId),
      })

      queryClient.setQueriesData<PaginatedResponse<Card>>(
        { queryKey: boardsQueryKeys.boardCards(boardId) },
        (previous) => {
          if (!previous) {
            return previous
          }

          const updatedItems = previous.items.map((card) =>
            card.id === id
              ? {
                  ...card,
                  columnId: data.columnId,
                  position: optimisticPosition ?? data.position,
                }
              : card,
          )

          return {
            ...previous,
            items: updatedItems,
          }
        },
      )

      return { previousBoardCardQueries }
    },
    onError: (_, __, context) => {
      if (!context) {
        return
      }

      context.previousBoardCardQueries.forEach(([queryKey, queryData]) => {
        queryClient.setQueryData(queryKey, queryData)
      })
    },
    onSuccess: (card) => {
      queryClient.setQueryData(cardsQueryKeys.card(card.id), card)
    },
    onSettled: (_, __, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardCards(variables.boardId) })
    },
  })
}

export function useAssignTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, tagId }: { cardId: Guid; tagId: Guid }) => assignTag(cardId, tagId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUnassignTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, tagId }: { cardId: Guid; tagId: Guid }) => unassignTag(cardId, tagId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useAssignUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, userId }: { cardId: Guid; userId: Guid }) => assignUser(cardId, userId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUnassignUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, userId }: { cardId: Guid; userId: Guid }) => unassignUser(cardId, userId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useAddAttachment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, file }: { cardId: Guid; file: File }) => addAttachment(cardId, file),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useRemoveAttachment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id }: { id: Guid; cardId: Guid }) => removeAttachment(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.attachmentUrl(variables.id) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useAttachmentUrl(id: Guid | undefined) {
  return useQuery({
    queryKey: id ? cardsQueryKeys.attachmentUrl(id) : ['attachments', 'url'],
    queryFn: () => getAttachmentUrl(id as Guid),
    enabled: Boolean(id),
  })
}

export function useCreateSubtask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, data }: { cardId: Guid; data: CreateSubtaskRequest }) => createSubtask(cardId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useUpdateSubtask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; cardId: Guid; data: UpdateSubtaskRequest }) => updateSubtask(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}

export function useDeleteSubtask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id }: { id: Guid; cardId: Guid }) => deleteSubtask(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardActivities(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boards })
      queryClient.invalidateQueries({ queryKey: projectsQueryKeys.projects })
    },
  })
}
