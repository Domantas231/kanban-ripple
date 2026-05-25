import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined'
import { useProject, useProjectActivities, useProjectMembers, useSwimlaneView } from '@/features/projects'
import { useAuthStore } from '@/features/auth'
import { CardDetailDialog } from '@/features/cards'
import { EmptyState } from '@/components/feedback/EmptyState'
import { OverviewBoardCards } from '@/features/overview/components/OverviewBoardCards'
import { OverviewOverdueCards } from '@/features/overview/components/OverviewOverdueCards'
import { OverviewUpcomingDeadlines } from '@/features/overview/components/OverviewUpcomingDeadlines'
import { OverviewTeamWorkload } from '@/features/overview/components/OverviewTeamWorkload'
import { OverviewUnassignedCards } from '@/features/overview/components/OverviewUnassignedCards'
import { OverviewRecentActivity } from '@/features/overview/components/OverviewRecentActivity'
import { OverviewTagsBreakdown } from '@/features/overview/components/OverviewTagsBreakdown'
import { OverviewSkeleton } from '@/features/overview/components/OverviewSkeleton'
import {
  flattenSwimlaneCards,
  getOverdueCards,
  getUpcomingCards,
  getUnassignedCards,
  type FlatCard,
} from '@/features/overview/utils/overviewUtils'
import type { Guid, ProjectActivity, ProjectRole } from '@/lib/types'

const PAGE_SPACING = { xs: 3.5, md: 5 }

interface OverviewPageProps {
  projectId: string
}

export function OverviewPage({ projectId }: OverviewPageProps) {
  const navigate = useNavigate()
  const projectQuery = useProject(projectId)
  const swimlaneQuery = useSwimlaneView(projectId)
  const membersQuery = useProjectMembers(projectId)
  const currentUserId = useAuthStore((state) => state.user?.id)

  const boards = swimlaneQuery.data?.boards ?? []
  const allCards = useMemo(() => flattenSwimlaneCards(boards), [boards])
  const overdueCards = useMemo(() => getOverdueCards(allCards), [allCards])
  const upcomingCards = useMemo(() => getUpcomingCards(allCards, 14), [allCards])
  const unassignedCards = useMemo(() => getUnassignedCards(allCards), [allCards])
  const activitiesQuery = useProjectActivities(projectId)
  const recentItems = activitiesQuery.data ?? []

  const project = projectQuery.data
  const members = useMemo(() => membersQuery.data ?? [], [membersQuery.data])

  const currentUserRole = useMemo(() => {
    if (!currentUserId) return undefined
    if (project?.ownerId === currentUserId) return 0 as ProjectRole
    return members.find((member) => member.userId === currentUserId)?.role
  }, [currentUserId, members, project?.ownerId])

  const canManageCards = currentUserRole !== undefined && currentUserRole <= 2

  const [selectedCard, setSelectedCard] = useState<{ cardId: Guid; boardId: Guid } | null>(null)

  const cardBoardLookup = useMemo(() => {
    const map = new Map<Guid, Guid>()
    for (const card of allCards) {
      map.set(card.id, card.boardId)
    }
    return map
  }, [allCards])

  const handleCardClick = useCallback((card: FlatCard) => {
    setSelectedCard({ cardId: card.id, boardId: card.boardId })
  }, [])

  const handleRecentItemClick = useCallback(
    (item: ProjectActivity) => {
      if (item.entityType === 'card' && item.cardId) {
        const boardId = item.boardId ?? cardBoardLookup.get(item.cardId)
        if (boardId) {
          setSelectedCard({ cardId: item.cardId, boardId })
          return
        }
      }
      if (item.entityType === 'board' && item.boardId) {
        void navigate({
          to: '/projects/$projectId/boards/$boardId',
          params: { projectId, boardId: item.boardId },
        })
      } else if (item.entityType === 'workspace') {
        void navigate({
          to: '/projects/$projectId/settings',
          params: { projectId },
        })
      }
    },
    [cardBoardLookup, navigate, projectId],
  )

  if (swimlaneQuery.isLoading) {
    return (
      <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
        <Stack spacing={4}>
          <OverviewHeader />
          <OverviewSkeleton />
        </Stack>
      </Box>
    )
  }

  if (swimlaneQuery.isError) {
    return (
      <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
        <Alert severity="error">Unable to load overview.</Alert>
      </Box>
    )
  }

  if (boards.length === 0) {
    return (
      <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
        <Stack spacing={4}>
          <OverviewHeader showSubtitle={false} />
          <EmptyState
            icon={ViewColumnOutlinedIcon}
            title="No boards yet"
            description="Create your first board to begin."
            compact
          />
        </Stack>
      </Box>
    )
  }

  return (
    <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
      <Stack spacing={PAGE_SPACING}>
        <OverviewHeader />

        <Box component="section">
          <SectionLabel>Boards</SectionLabel>
          <OverviewBoardCards projectId={projectId} boards={boards} />
        </Box>

        {(overdueCards.length > 0 ||
          upcomingCards.length > 0 ||
          recentItems.length > 0 ||
          unassignedCards.length > 0) && (
          <Box component="section">
            <SectionLabel>Tasks & Activity</SectionLabel>
            <Stack spacing={{ xs: 3, md: 4 }}>
              {(overdueCards.length > 0 || upcomingCards.length > 0) && (
                <TwoColumnGrid>
                  {overdueCards.length > 0 && (
                    <OverviewOverdueCards cards={overdueCards} onCardClick={handleCardClick} />
                  )}
                  {upcomingCards.length > 0 && (
                    <OverviewUpcomingDeadlines cards={upcomingCards} onCardClick={handleCardClick} />
                  )}
                </TwoColumnGrid>
              )}

              {(recentItems.length > 0 || unassignedCards.length > 0) && (
                <TwoColumnGrid>
                  {recentItems.length > 0 && (
                    <OverviewRecentActivity items={recentItems} onItemClick={handleRecentItemClick} />
                  )}
                  {unassignedCards.length > 0 && (
                    <OverviewUnassignedCards cards={unassignedCards} onCardClick={handleCardClick} />
                  )}
                </TwoColumnGrid>
              )}
            </Stack>
          </Box>
        )}

        <Box component="section">
          <SectionLabel>Team & Tags</SectionLabel>
          <TwoColumnGrid>
            <OverviewTeamWorkload cards={allCards} />
            <OverviewTagsBreakdown cards={allCards} />
          </TwoColumnGrid>
        </Box>
      </Stack>

      <CardDetailDialog
        open={Boolean(selectedCard)}
        cardId={selectedCard?.cardId ?? null}
        boardId={selectedCard?.boardId ?? ''}
        members={members}
        canManageCards={canManageCards}
        currentUserRole={currentUserRole}
        onClose={() => setSelectedCard(null)}
      />
    </Box>
  )
}

function OverviewHeader({ showSubtitle = true }: { showSubtitle?: boolean }) {
  return (
    <Box>
      <Typography variant="h3" sx={{ fontWeight: 700, mb: 0.5 }}>
        Overview
      </Typography>
      {showSubtitle && (
        <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 520 }}>
          A high-level snapshot of your workspace.
        </Typography>
      )}
    </Box>
  )
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <Typography
      variant="overline"
      sx={{ color: 'text.secondary', letterSpacing: 1.5, mb: 2, display: 'block' }}
    >
      {children}
    </Typography>
  )
}

function TwoColumnGrid({ children }: { children: React.ReactNode }) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'minmax(0, 1fr) minmax(0, 1fr)' },
        gap: { xs: 3, md: 4 },
      }}
    >
      {children}
    </Box>
  )
}
