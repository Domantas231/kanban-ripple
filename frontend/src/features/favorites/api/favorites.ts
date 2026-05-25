import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'
import { favoritesQueryKeys } from '@/features/favorites/api/query-keys'
import { subscriptionsQueryKeys } from '@/features/subscriptions'
import type { EntityType, FavoriteDto, Guid } from '@/lib/types'

// `crypto.randomUUID` is only defined in secure contexts (HTTPS / localhost).
// Mobile devices hitting the dev server over LAN HTTP would otherwise throw
// inside the optimistic update, cancelling the mutation before it fires.
function makeOptimisticId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return `optimistic-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

export async function getFavorites(): Promise<FavoriteDto[]> {
  const response = await apiClient.get<FavoriteDto[]>('/api/favorites')
  return response.data
}

export async function toggleFavorite(entityType: EntityType, entityId: Guid): Promise<FavoriteDto> {
  const response = await apiClient.post<FavoriteDto>('/api/favorites/toggle', {
    entityType,
    entityId,
  })
  return response.data
}

export function useFavorites() {
  return useQuery({
    queryKey: favoritesQueryKeys.favorites,
    queryFn: getFavorites,
  })
}

export function useToggleFavorite() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ entityType, entityId }: { entityType: EntityType; entityId: Guid }) =>
      toggleFavorite(entityType, entityId),
    onMutate: async ({ entityType, entityId }) => {
      await queryClient.cancelQueries({ queryKey: favoritesQueryKeys.favorites })

      const previous = queryClient.getQueryData<FavoriteDto[]>(favoritesQueryKeys.favorites)

      queryClient.setQueryData<FavoriteDto[]>(favoritesQueryKeys.favorites, (old) => {
        if (!old) return old
        const exists = old.some((f) => f.entityType === entityType && f.entityId === entityId)
        if (exists) {
          return old.filter((f) => !(f.entityType === entityType && f.entityId === entityId))
        }
        return [
          {
            id: makeOptimisticId(),
            entityType,
            entityId,
            createdAt: new Date().toISOString(),
          },
          ...old,
        ]
      })

      return { previous }
    },
    onError: (_, __, context) => {
      if (context?.previous) {
        queryClient.setQueryData(favoritesQueryKeys.favorites, context.previous)
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: favoritesQueryKeys.favorites })
      queryClient.invalidateQueries({ queryKey: subscriptionsQueryKeys.subscriptions })
    },
  })
}
