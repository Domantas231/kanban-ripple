import { useEffect, useMemo } from 'react'
import { createFileRoute, useNavigate, useRouterState } from '@tanstack/react-router'
import { useAuthStore } from '@/features/auth'
import { OverviewPage } from '@/features/overview'
import { isManagerPlus, useProject, useProjectMembers } from '@/features/projects'
import type { ProjectRole } from '@/lib/types'

export const Route = createFileRoute('/_authenticated/projects/$projectId/swimlane')({
  component: SwimlaneRoute,
})

function SwimlaneRoute() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const projectId = pathname.match(/^\/projects\/([^/]+)/)?.[1] ?? ''
  const navigate = useNavigate()
  const currentUserId = useAuthStore((state) => state.user?.id)
  const projectQuery = useProject(projectId || undefined)
  const membersQuery = useProjectMembers(projectId || undefined)

  const currentUserRole = useMemo<ProjectRole | undefined>(() => {
    if (!currentUserId) return undefined
    if (projectQuery.data?.ownerId === currentUserId) return 0 as ProjectRole
    return membersQuery.data?.find((member) => member.userId === currentUserId)?.role
  }, [currentUserId, projectQuery.data?.ownerId, membersQuery.data])

  const isAuthorized = isManagerPlus(currentUserRole)
  const isResolving =
    projectQuery.isLoading || membersQuery.isLoading || currentUserRole === undefined

  useEffect(() => {
    if (!projectId || isResolving || isAuthorized) return
    void navigate({ to: `/projects/${projectId}`, replace: true })
  }, [projectId, isResolving, isAuthorized, navigate])

  if (!isAuthorized) return null

  return <OverviewPage projectId={projectId} />
}
