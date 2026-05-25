import { useMemo } from 'react'
import { useQueries } from '@tanstack/react-query'
import {
  useArchivedBoards,
  usePurgeBoard,
  usePurgeColumn,
  useRestoreBoard,
  useRestoreColumn,
} from '@/features/boards'
import { useArchivedCards, usePurgeCard, useRestoreCard } from '@/features/cards'
import {
  getProjectMembers,
  useAllProjects,
  useArchivedProjects,
  usePurgeProject,
  useRestoreProject,
} from '@/features/projects'
import { projectsQueryKeys } from '@/features/projects'
import { useAuthStore } from '@/features/auth'
import type { Board, Card as KanbanCard, Guid, Project } from '@/lib/types'
import { isMemberPlus } from '@/features/archive/utils/archiveFormatters'

export type ColumnArchiveItem = {
  id: Guid
  name: string
  boardId?: Guid
  projectId?: Guid
  deletedAt?: string | null
}

export interface UseArchivePageDataParams {
  projectsPage: number
  cardsPage: number
  selectedProjectId: string
}

export function useArchivePageData({ projectsPage, cardsPage, selectedProjectId }: UseArchivePageDataParams) {
  const currentUserId = useAuthStore((state) => state.user?.id)

  const archivedProjectsQuery = useArchivedProjects(projectsPage)
  const allProjectsQuery = useAllProjects()
  const archivedBoardsQuery = useArchivedBoards()
  const archivedCardsQuery = useArchivedCards(cardsPage)

  const restoreProjectMutation = useRestoreProject()
  const restoreBoardMutation = useRestoreBoard()
  const restoreColumnMutation = useRestoreColumn()
  const restoreCardMutation = useRestoreCard()

  const purgeProjectMutation = usePurgeProject()
  const purgeBoardMutation = usePurgeBoard()
  const purgeColumnMutation = usePurgeColumn()
  const purgeCardMutation = usePurgeCard()

  const archivedProjects = archivedProjectsQuery.data?.items ?? []
  const allProjects = allProjectsQuery.data ?? []
  const archivedBoards = archivedBoardsQuery.data ?? []
  const archivedCards = archivedCardsQuery.data?.items ?? []

  const projectOptions = useMemo(() => {
    const byId = new Map<Guid, string>()

    allProjects.forEach((project) => {
      byId.set(project.id, project.name)
    })

    archivedProjects.forEach((project) => {
      if (!byId.has(project.id)) {
        byId.set(project.id, project.name)
      }
    })

    archivedBoards.forEach((board) => {
      if (!byId.has(board.projectId)) {
        byId.set(board.projectId, board.projectId)
      }
    })

    const cards = archivedCards as Array<KanbanCard & { column?: { board?: { projectId?: Guid } } }>
    cards.forEach((card) => {
      const projectId = card.column?.board?.projectId
      if (projectId && !byId.has(projectId)) {
        byId.set(projectId, projectId)
      }
    })

    return [{ id: 'all', name: 'All workspaces' }, ...Array.from(byId, ([id, name]) => ({ id, name }))]
  }, [allProjects, archivedBoards, archivedCards, archivedProjects])

  const boardById = useMemo(() => {
    const byId = new Map<Guid, Board>()
    archivedBoards.forEach((board) => {
      byId.set(board.id, board)
    })
    return byId
  }, [archivedBoards])

  const projectById = useMemo(() => {
    const byId = new Map<Guid, Project>()
    allProjects.forEach((project) => {
      byId.set(project.id, project)
    })
    archivedProjects.forEach((project) => {
      if (!byId.has(project.id)) {
        byId.set(project.id, project)
      }
    })
    return byId
  }, [allProjects, archivedProjects])

  const filteredProjects = useMemo(() => {
    if (selectedProjectId === 'all') return archivedProjects
    return archivedProjects.filter((project) => project.id === selectedProjectId)
  }, [archivedProjects, selectedProjectId])

  const projectIdsForAccess = useMemo(() => {
    const ids = new Set<Guid>()

    archivedProjects.forEach((project) => {
      ids.add(project.id)
    })

    archivedBoards.forEach((board) => {
      ids.add(board.projectId)
    })

    const cards = archivedCards as Array<KanbanCard & { column?: { board?: { projectId?: Guid } } }>
    cards.forEach((card) => {
      const projectId = card.column?.board?.projectId
      if (projectId) {
        ids.add(projectId)
      }
    })

    return Array.from(ids)
  }, [archivedBoards, archivedCards, archivedProjects])

  const membershipQueries = useQueries({
    queries: projectIdsForAccess.map((projectId) => ({
      queryKey: projectsQueryKeys.projectMembers(projectId),
      queryFn: () => getProjectMembers(projectId),
      enabled: Boolean(currentUserId),
    })),
  })

  const restoreAccessByProjectId = useMemo(() => {
    const access = new Map<Guid, boolean>()

    projectIdsForAccess.forEach((projectId, index) => {
      const project = projectById.get(projectId)
      if (!currentUserId) {
        access.set(projectId, false)
        return
      }

      if (project?.ownerId === currentUserId) {
        access.set(projectId, true)
        return
      }

      const members = membershipQueries[index]?.data ?? []
      const role = members.find((member) => member.userId === currentUserId)?.role
      access.set(projectId, isMemberPlus(role))
    })

    return access
  }, [currentUserId, membershipQueries, projectById, projectIdsForAccess])

  const ownershipByProjectId = useMemo(() => {
    const owned = new Map<Guid, boolean>()
    projectIdsForAccess.forEach((projectId) => {
      const project = projectById.get(projectId)
      owned.set(projectId, Boolean(currentUserId) && project?.ownerId === currentUserId)
    })
    return owned
  }, [currentUserId, projectById, projectIdsForAccess])

  const filteredBoards = useMemo(() => {
    if (selectedProjectId === 'all') return archivedBoards
    return archivedBoards.filter((board) => board.projectId === selectedProjectId)
  }, [archivedBoards, selectedProjectId])

  const filteredCards = useMemo(() => {
    if (selectedProjectId === 'all') return archivedCards
    const cards = archivedCards as Array<KanbanCard & { column?: { board?: { projectId?: Guid } } }>
    return cards.filter((card) => card.column?.board?.projectId === selectedProjectId)
  }, [archivedCards, selectedProjectId])

  const archivedColumns = useMemo<ColumnArchiveItem[]>(() => {
    const cards = archivedCards as Array<
      KanbanCard & {
        column?: { name?: string; boardId?: Guid; deletedAt?: string | null; board?: { id?: Guid; projectId?: Guid } }
      }
    >
    const byId = new Map<Guid, ColumnArchiveItem>()

    cards.forEach((card) => {
      const columnDeletedAt = card.column?.deletedAt
      if (!columnDeletedAt) return

      const columnId = card.column?.id ?? card.columnId
      const boardId = card.column?.boardId ?? card.column?.board?.id
      const board = boardId ? boardById.get(boardId) : undefined
      const projectId = card.column?.board?.projectId ?? board?.projectId
      const deletedAt = columnDeletedAt

      const existing = byId.get(columnId)
      if (!existing) {
        byId.set(columnId, {
          id: columnId,
          name: card.column?.name ?? `List ${columnId.slice(0, 8)}`,
          boardId,
          projectId,
          deletedAt,
        })
        return
      }

      if (!existing.projectId && projectId) {
        existing.projectId = projectId
      }

      if (deletedAt && (!existing.deletedAt || deletedAt > existing.deletedAt)) {
        existing.deletedAt = deletedAt
      }
    })

    const items = Array.from(byId.values())
    if (selectedProjectId === 'all') return items
    return items.filter((column) => column.projectId === selectedProjectId)
  }, [archivedCards, boardById, selectedProjectId])

  const projectsTotalCount = archivedProjectsQuery.data?.totalCount ?? 0
  const projectsPageSize = archivedProjectsQuery.data?.pageSize ?? 25
  const projectsTotalPages = Math.max(1, Math.ceil(projectsTotalCount / projectsPageSize))

  const cardsTotalCount = archivedCardsQuery.data?.totalCount ?? 0
  const cardsPageSize = archivedCardsQuery.data?.pageSize ?? 25
  const cardsTotalPages = Math.max(1, Math.ceil(cardsTotalCount / cardsPageSize))

  return {
    archivedProjectsQuery,
    archivedBoardsQuery,
    archivedCardsQuery,
    restoreProjectMutation,
    restoreBoardMutation,
    restoreColumnMutation,
    restoreCardMutation,
    purgeProjectMutation,
    purgeBoardMutation,
    purgeColumnMutation,
    purgeCardMutation,
    archivedCards,
    boardById,
    projectById,
    projectOptions,
    filteredProjects,
    filteredBoards,
    filteredCards,
    archivedColumns,
    restoreAccessByProjectId,
    ownershipByProjectId,
    projectsTotalCount,
    projectsTotalPages,
    cardsTotalCount,
    cardsTotalPages,
  }
}
