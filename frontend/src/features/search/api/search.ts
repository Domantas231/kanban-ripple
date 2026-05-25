import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { apiClient } from '@/lib/api-client'
import { searchQueryKeys } from '@/features/search/api/query-keys'
import { projectsQueryKeys } from '@/features/projects'
import { boardsQueryKeys } from '@/features/boards'
import type { Card, FilterCriteria, GlobalSearchResult, Guid, PaginatedResponse } from '@/lib/types'

const SEARCH_DEBOUNCE_MS = 300
const DEFAULT_SEARCH_PAGE_SIZE = 25

export async function globalSearch(query: string): Promise<GlobalSearchResult> {
  const response = await apiClient.get<GlobalSearchResult>('/api/search', {
    params: { q: query },
  })
  return response.data
}

export function useGlobalSearch(query: string) {
  const normalizedQuery = query.trim()
  const [debouncedQuery, setDebouncedQuery] = useState(normalizedQuery)

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedQuery(normalizedQuery)
    }, SEARCH_DEBOUNCE_MS)

    return () => {
      window.clearTimeout(timeoutId)
    }
  }, [normalizedQuery])

  return useQuery({
    queryKey: searchQueryKeys.globalSearch(debouncedQuery),
    queryFn: () => globalSearch(debouncedQuery),
    enabled: debouncedQuery.length > 0,
  })
}

export async function searchCards(
  projectId: Guid,
  query: string,
  page = 1,
  pageSize = DEFAULT_SEARCH_PAGE_SIZE,
): Promise<PaginatedResponse<Card>> {
  const response = await apiClient.get<PaginatedResponse<Card>>(`/api/projects/${projectId}/cards/search`, {
    params: {
      q: query,
      page,
      pageSize,
    },
  })

  return response.data
}

export async function filterCards(boardId: Guid, filters: FilterCriteria): Promise<Card[]> {
  const response = await apiClient.get<Card[]>(`/api/boards/${boardId}/cards/filter`, {
    params: {
      tagIds: toCsv(filters.tagIds),
      userIds: toCsv(filters.userIds),
    },
  })

  return response.data
}

export function useSearchCards(projectId: Guid | undefined, query: string, page = 1) {
  const normalizedQuery = query.trim()
  const [debouncedQuery, setDebouncedQuery] = useState(normalizedQuery)

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedQuery(normalizedQuery)
    }, SEARCH_DEBOUNCE_MS)

    return () => {
      window.clearTimeout(timeoutId)
    }
  }, [normalizedQuery])

  return useQuery({
    queryKey: projectId
      ? searchQueryKeys.searchCards(projectId, debouncedQuery, page, DEFAULT_SEARCH_PAGE_SIZE)
      : [...projectsQueryKeys.projects, 'cards', 'search'],
    queryFn: () => searchCards(projectId as Guid, debouncedQuery, page, DEFAULT_SEARCH_PAGE_SIZE),
    enabled: Boolean(projectId) && debouncedQuery.length > 0,
  })
}

export function useFilterCards(boardId: Guid | undefined, filters: FilterCriteria) {
  const normalizedFilters = useMemo(
    () => ({
      tagIds: normalizeGuidList(filters.tagIds),
      userIds: normalizeGuidList(filters.userIds),
    }),
    [filters.tagIds, filters.userIds],
  )

  const enabled = Boolean(boardId) && hasActiveFilters(normalizedFilters)

  return useQuery({
    queryKey: boardId
      ? searchQueryKeys.filteredBoardCards(
          boardId,
          normalizedFilters.tagIds,
          normalizedFilters.userIds,
        )
      : [...boardsQueryKeys.boards, 'cards', 'filter'],
    queryFn: () => filterCards(boardId as Guid, normalizedFilters),
    enabled,
  })
}

function hasActiveFilters(filters: FilterCriteria): boolean {
  return Boolean(filters.tagIds?.length || filters.userIds?.length)
}

function normalizeGuidList(values: Guid[] | undefined): Guid[] | undefined {
  if (!values || values.length === 0) {
    return undefined
  }

  return [...values].sort((a, b) => a.localeCompare(b))
}

function toCsv(values: Guid[] | undefined): string | undefined {
  if (!values || values.length === 0) {
    return undefined
  }

  return values.join(',')
}
