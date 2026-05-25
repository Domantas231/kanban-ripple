import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { cardsQueryKeys } from '@/features/cards/api/query-keys'
import type { Comment, Guid } from '@/lib/types'

export async function getComments(cardId: Guid): Promise<Comment[]> {
  const response = await apiClient.get<Comment[]>(`/api/cards/${cardId}/comments`)
  return response.data
}

export async function createComment(cardId: Guid, content: string): Promise<Comment> {
  const response = await apiClient.post<Comment>(`/api/cards/${cardId}/comments`, { content })
  return response.data
}

export async function updateComment(id: Guid, content: string): Promise<Comment> {
  const response = await apiClient.put<Comment>(`/api/comments/${id}`, { content })
  return response.data
}

export async function deleteComment(id: Guid): Promise<void> {
  await apiClient.delete(`/api/comments/${id}`)
}

export function useComments(cardId: Guid | undefined) {
  return useQuery({
    queryKey: cardId ? cardsQueryKeys.cardComments(cardId) : ['cards', 'comments'],
    queryFn: () => getComments(cardId as Guid),
    enabled: Boolean(cardId),
  })
}

export function useCreateComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ cardId, content }: { cardId: Guid; content: string }) => createComment(cardId, content),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardComments(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
    },
  })
}

export function useUpdateComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, content }: { id: Guid; content: string; cardId: Guid }) => updateComment(id, content),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardComments(variables.cardId) })
    },
  })
}

export function useDeleteComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id }: { id: Guid; cardId: Guid }) => deleteComment(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.cardComments(variables.cardId) })
      queryClient.invalidateQueries({ queryKey: cardsQueryKeys.card(variables.cardId) })
    },
  })
}
