import { useEffect, useMemo, useState } from 'react'
import { Outlet, useRouterState } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import { ProjectDashboard } from '@/features/boards'
import { useProject, useProjectMembers } from '@/features/projects/api/projects'
import { useAuthStore } from '@/features/auth'
import { useRealtimeStore } from '@/features/realtime'
import { signalRService } from '@/features/realtime'
import type { ProjectRole } from '@/lib/types'

interface ProjectDetailPageProps {
  projectId: string
}

export function ProjectDetailPage({ projectId }: ProjectDetailPageProps) {
  const currentUserId = useAuthStore((state) => state.user?.id)
  const connectionState = useRealtimeStore((state) => state.connectionState)
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  const projectQuery = useProject(projectId)
  const membersQuery = useProjectMembers(projectId)

  const [realtimeError, setRealtimeError] = useState<string | null>(null)
  const [prevConnectionState, setPrevConnectionState] = useState(connectionState)

  if (prevConnectionState !== connectionState) {
    setPrevConnectionState(connectionState)
    if (connectionState === 'connected') {
      setRealtimeError(null)
    }
  }

  useEffect(() => {
    let isActive = true

    const joinProjectGroup = async () => {
      try {
        await signalRService.joinProject(projectId)
        if (isActive) {
          setRealtimeError(null)
        }
      } catch {
        if (isActive) {
          setRealtimeError(
            'Unable to join realtime updates for this workspace. Changes may appear with a delay.',
          )
        }
      }
    }

    void joinProjectGroup()

    return () => {
      isActive = false
      void signalRService.leaveProject(projectId).catch(() => {})
    }
  }, [projectId])

  const currentUserRole = useMemo(() => {
    if (!currentUserId) return undefined
    if (projectQuery.data?.ownerId === currentUserId) return 0 as ProjectRole
    return membersQuery.data?.find((member) => member.userId === currentUserId)?.role
  }, [currentUserId, membersQuery.data, projectQuery.data?.ownerId])

  const canManageBoards = currentUserRole !== undefined && currentUserRole <= 2

  const showReconnectingIndicator = connectionState === 'reconnecting'
  const showDisconnectedIndicator = connectionState === 'disconnected' && !realtimeError

  const hasRealtimeAlerts =
    showReconnectingIndicator || showDisconnectedIndicator || Boolean(realtimeError)

  const isExactProjectPath = pathname === `/projects/${projectId}`

  return (
    <>
      {hasRealtimeAlerts ? (
        <Box sx={{ px: 3, pt: 1.5 }}>
          <Stack spacing={1.5}>
            {showReconnectingIndicator ? (
              <Alert severity="warning">Realtime connection lost. Reconnecting...</Alert>
            ) : null}
            {showDisconnectedIndicator ? (
              <Alert severity="warning">Realtime updates are currently offline.</Alert>
            ) : null}
            {realtimeError ? <Alert severity="error">{realtimeError}</Alert> : null}
          </Stack>
        </Box>
      ) : null}

      {isExactProjectPath ? (
        <ProjectDashboard projectId={projectId} canManageBoards={canManageBoards} />
      ) : (
        <Outlet />
      )}
    </>
  )
}
