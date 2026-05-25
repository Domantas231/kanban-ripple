import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { subscriptionsQueryKeys } from '@/features/subscriptions/api/query-keys'
import type { EntityType, Guid, MySubscriptionDto, Subscription } from '@/lib/types'

export type SubscribeRequest = {
  entityType: EntityType
  entityId: Guid
}

export type UnsubscribeByEntityRequest = {
  entityType: EntityType
  entityId: Guid
}

function getEntityTypeName(entityType: EntityType): 'Card' | 'Column' | 'Project' | 'Board' {
  switch (entityType) {
    case 0:
      return 'Card'
    case 1:
      return 'Column'
    case 3:
      return 'Board'
    default:
      return 'Project'
  }
}

export async function subscribe(request: SubscribeRequest): Promise<Subscription> {
  const response = await apiClient.post<Subscription>('/api/subscriptions', request)
  return response.data
}

export async function unsubscribe(id: Guid): Promise<void> {
  await apiClient.delete(`/api/subscriptions/${id}`)
}

export async function unsubscribeByEntity(request: UnsubscribeByEntityRequest): Promise<void> {
  await apiClient.delete('/api/subscriptions', {
    params: {
      entityType: getEntityTypeName(request.entityType),
      entityId: request.entityId,
    },
  })
}

export async function getMySubscriptions(): Promise<MySubscriptionDto[]> {
  const response = await apiClient.get<MySubscriptionDto[]>('/api/subscriptions/mine')
  return response.data
}

export async function getCardSubscriptions(cardId: Guid): Promise<Guid[]> {
  const response = await apiClient.get<Guid[]>(`/api/cards/${cardId}/subscriptions`)
  return response.data
}

export async function getColumnSubscriptions(columnId: Guid): Promise<Guid[]> {
  const response = await apiClient.get<Guid[]>(`/api/columns/${columnId}/subscriptions`)
  return response.data
}

export async function getProjectSubscriptions(projectId: Guid): Promise<Guid[]> {
  const response = await apiClient.get<Guid[]>(`/api/projects/${projectId}/subscriptions`)
  return response.data
}

export async function getBoardSubscriptions(boardId: Guid): Promise<Guid[]> {
  const response = await apiClient.get<Guid[]>(`/api/boards/${boardId}/subscriptions`)
  return response.data
}

export function useSubscribe() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: subscribe,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: subscriptionsQueryKeys.subscriptions })
    },
  })
}

export function useUnsubscribe() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: Guid) => unsubscribe(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: subscriptionsQueryKeys.subscriptions })
    },
  })
}

export function useUnsubscribeByEntity() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: unsubscribeByEntity,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: subscriptionsQueryKeys.subscriptions })
    },
  })
}

export function useCardSubscriptions(cardId: Guid | undefined) {
  return useQuery({
    queryKey: cardId ? subscriptionsQueryKeys.cardSubscriptions(cardId) : [...subscriptionsQueryKeys.subscriptions, 'card'],
    queryFn: () => getCardSubscriptions(cardId as Guid),
    enabled: Boolean(cardId),
  })
}

export function useColumnSubscriptions(columnId: Guid | undefined) {
  return useQuery({
    queryKey: columnId
      ? subscriptionsQueryKeys.columnSubscriptions(columnId)
      : [...subscriptionsQueryKeys.subscriptions, 'column'],
    queryFn: () => getColumnSubscriptions(columnId as Guid),
    enabled: Boolean(columnId),
  })
}

export function useProjectSubscriptions(projectId: Guid | undefined) {
  return useQuery({
    queryKey: projectId
      ? subscriptionsQueryKeys.projectSubscriptions(projectId)
      : [...subscriptionsQueryKeys.subscriptions, 'project'],
    queryFn: () => getProjectSubscriptions(projectId as Guid),
    enabled: Boolean(projectId),
  })
}

export function useBoardSubscriptions(boardId: Guid | undefined) {
  return useQuery({
    queryKey: boardId
      ? subscriptionsQueryKeys.boardSubscriptions(boardId)
      : [...subscriptionsQueryKeys.subscriptions, 'board'],
    queryFn: () => getBoardSubscriptions(boardId as Guid),
    enabled: Boolean(boardId),
  })
}

export function useMySubscriptions() {
  return useQuery({
    queryKey: subscriptionsQueryKeys.mySubscriptions,
    queryFn: getMySubscriptions,
  })
}
