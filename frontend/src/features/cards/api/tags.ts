import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { cardsQueryKeys } from '@/features/cards/api/query-keys'
import { boardsQueryKeys } from '@/features/boards'
import type { Guid, Tag } from '@/lib/types'

export type CreateTagRequest = {
  name: string
  color: string
}

export type UpdateTagRequest = {
  name: string
  color: string
}

export async function getBoardTags(boardId: Guid): Promise<Tag[]> {
  const response = await apiClient.get<Tag[]>(`/api/boards/${boardId}/tags`)
  return response.data
}

export async function createTag(boardId: Guid, request: CreateTagRequest): Promise<Tag> {
  const response = await apiClient.post<Tag>(`/api/boards/${boardId}/tags`, request)
  return response.data
}

export async function updateTag(id: Guid, request: UpdateTagRequest): Promise<Tag> {
  const response = await apiClient.put<Tag>(`/api/tags/${id}`, request)
  return response.data
}

export async function deleteTag(id: Guid): Promise<void> {
  await apiClient.delete(`/api/tags/${id}`)
}

function useTags(boardId: Guid | undefined) {
  return useQuery({
    queryKey: boardId ? cardsQueryKeys.boardTags(boardId) : [...boardsQueryKeys.boards, 'tags'],
    queryFn: () => getBoardTags(boardId as Guid),
    enabled: Boolean(boardId),
  })
}

export function useCreateTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ boardId, data }: { boardId: Guid; data: CreateTagRequest }) => createTag(boardId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.boardTags(variables.boardId) })
    },
  })
}

export function useUpdateTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: Guid; boardId: Guid; data: UpdateTagRequest }) => updateTag(id, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.boardTags(variables.boardId) })
    },
  })
}

export function useDeleteTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id }: { id: Guid; boardId: Guid }) => deleteTag(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.boardTags(variables.boardId) })
    },
  })
}

export const useBoardTags = useTags
