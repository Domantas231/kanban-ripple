import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import FormControl from '@mui/material/FormControl'
import InputLabel from '@mui/material/InputLabel'
import MenuItem from '@mui/material/MenuItem'
import Pagination from '@mui/material/Pagination'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Typography from '@mui/material/Typography'
import type { Card as KanbanCard, Guid } from '@/lib/types'
import { ArchiveItemCard } from '@/features/archive/components/ArchiveItemCard'
import { ArchiveListState } from '@/features/archive/components/ArchiveListState'
import { useArchivePageData } from '@/features/archive/hooks/useArchivePageData'
import { useUiStore } from '@/stores/uiStore'

type ArchiveTab = 'projects' | 'boards' | 'columns' | 'cards'

export function ArchivePage() {
  const [activeTab, setActiveTab] = useState<ArchiveTab>('projects')
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all')
  const [projectsPage, setProjectsPage] = useState(1)
  const [cardsPage, setCardsPage] = useState(1)
  const enqueueToast = useUiStore((state) => state.enqueueToast)

  const {
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
  } = useArchivePageData({ projectsPage, cardsPage, selectedProjectId })

  return (
    <Box sx={{ px: 3, pb: 3 }}>
      <Stack spacing={3}>
        <Typography variant="h4">Archive / Trash</Typography>

        <Alert severity="info">Archived items will be permanently deleted after 7 days.</Alert>

        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'center' }}>
          <FormControl sx={{ minWidth: 260 }}>
            <InputLabel id="archive-project-filter-label">Workspace</InputLabel>
            <Select
              labelId="archive-project-filter-label"
              value={selectedProjectId}
              label="Workspace"
              onChange={(event) => setSelectedProjectId(event.target.value)}
            >
              {projectOptions.map((project) => (
                <MenuItem key={project.id} value={project.id}>
                  {project.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>

        <Tabs
          value={activeTab}
          onChange={(_, value: ArchiveTab) => setActiveTab(value)}
          variant="scrollable"
          allowScrollButtonsMobile
        >
          <Tab value="projects" label="Workspaces" />
          <Tab value="boards" label="Boards" />
          <Tab value="columns" label="Lists" />
          <Tab value="cards" label="Tasks" />
        </Tabs>

        {activeTab === 'projects' ? (
          <ArchiveListState
            isLoading={archivedProjectsQuery.isLoading}
            isError={archivedProjectsQuery.isError}
            loadingText="Loading archived workspaces..."
            errorText="Unable to load archived workspaces."
            emptyText="No archived workspaces found."
            hasItems={filteredProjects.length > 0}
          >
            <Stack spacing={1.5}>
              {filteredProjects.map((project) => {
                const canRestore = restoreAccessByProjectId.get(project.id) ?? false
                const isOwner = ownershipByProjectId.get(project.id) ?? false
                return (
                  <ArchiveItemCard
                    key={project.id}
                    name={project.name}
                    location="Workspace"
                    deletedAt={project.deletedAt}
                    canRestore={canRestore}
                    onRestore={() => restoreProjectMutation.mutate(project.id)}
                    restorePending={restoreProjectMutation.isPending}
                    canDelete={isOwner}
                    onDelete={() => purgeProjectMutation.mutate(project.id)}
                    deletePending={purgeProjectMutation.isPending}
                  />
                )
              })}
            </Stack>
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Pagination
                page={projectsPage}
                count={projectsTotalPages}
                onChange={(_, value) => setProjectsPage(value)}
                disabled={archivedProjectsQuery.isLoading || projectsTotalCount === 0}
              />
            </Box>
          </ArchiveListState>
        ) : null}

        {activeTab === 'boards' ? (
          <ArchiveListState
            isLoading={archivedBoardsQuery.isLoading}
            isError={archivedBoardsQuery.isError}
            loadingText="Loading archived boards..."
            errorText="Unable to load archived boards."
            emptyText="No archived boards found."
            hasItems={filteredBoards.length > 0}
          >
            <Stack spacing={1.5}>
              {filteredBoards.map((board) => {
                const canManage = restoreAccessByProjectId.get(board.projectId) ?? false
                return (
                  <ArchiveItemCard
                    key={board.id}
                    name={board.name}
                    location={`Workspace: ${projectById.get(board.projectId)?.name ?? board.projectId}`}
                    deletedAt={board.deletedAt}
                    canRestore={canManage}
                    onRestore={() => restoreBoardMutation.mutate(board.id)}
                    restorePending={restoreBoardMutation.isPending}
                    canDelete={canManage}
                    onDelete={() => purgeBoardMutation.mutate(board.id)}
                    deletePending={purgeBoardMutation.isPending}
                  />
                )
              })}
            </Stack>
          </ArchiveListState>
        ) : null}

        {activeTab === 'columns' ? (
          <ArchiveListState
            isLoading={archivedCardsQuery.isLoading}
            isError={archivedCardsQuery.isError}
            loadingText="Loading archived lists..."
            errorText="Unable to load archived lists."
            emptyText="No archived lists found."
            hasItems={archivedColumns.length > 0}
          >
            <Stack spacing={1.5}>
              {archivedColumns.map((column) => {
                const canManage = column.projectId
                  ? (restoreAccessByProjectId.get(column.projectId) ?? false)
                  : false
                return (
                  <ArchiveItemCard
                    key={column.id}
                    name={column.name}
                    location={
                      column.boardId
                        ? `Board: ${boardById.get(column.boardId)?.name ?? column.boardId}`
                        : 'Board: Unknown'
                    }
                    deletedAt={column.deletedAt}
                    canRestore={canManage}
                    onRestore={() => restoreColumnMutation.mutate(column.id)}
                    restorePending={restoreColumnMutation.isPending}
                    canDelete={canManage}
                    onDelete={() => purgeColumnMutation.mutate(column.id)}
                    deletePending={purgeColumnMutation.isPending}
                  />
                )
              })}
            </Stack>
          </ArchiveListState>
        ) : null}

        {activeTab === 'cards' ? (
          <ArchiveListState
            isLoading={archivedCardsQuery.isLoading}
            isError={archivedCardsQuery.isError}
            loadingText="Loading archived tasks..."
            errorText="Unable to load archived tasks."
            emptyText="No archived tasks found."
            hasItems={filteredCards.length > 0}
          >
            <Stack spacing={1.5}>
              {filteredCards.map((card) => {
                const typedCard = card as KanbanCard & {
                  column?: { name?: string; deletedAt?: string | null; board?: { name?: string; projectId?: Guid } }
                }
                const location = typedCard.column?.board?.name
                  ? `Board: ${typedCard.column.board.name} • List: ${typedCard.column?.name ?? typedCard.columnId}`
                  : `List ID: ${typedCard.columnId}`
                const projectId = typedCard.column?.board?.projectId
                const hasRestoreAccess =
                  Boolean(projectId) && (projectId ? (restoreAccessByProjectId.get(projectId) ?? false) : false)
                const columnArchived = Boolean(typedCard.column?.deletedAt)
                const canRestoreCard = hasRestoreAccess

                const canDeleteCard = hasRestoreAccess

                return (
                  <ArchiveItemCard
                    key={typedCard.id}
                    name={typedCard.title}
                    location={location}
                    deletedAt={typedCard.deletedAt}
                    canRestore={canRestoreCard}
                    restoreDisabledReason={
                      columnArchived ? 'Restore the list first to unarchive this task.' : null
                    }
                    onRestore={() => {
                      if (columnArchived) {
                        enqueueToast({
                          message: 'Cannot restore this task because its list is archived. Restore the list first.',
                          severity: 'error',
                        })
                        return
                      }
                      restoreCardMutation.mutate(typedCard.id)
                    }}
                    restorePending={restoreCardMutation.isPending}
                    canDelete={canDeleteCard}
                    onDelete={() => purgeCardMutation.mutate(typedCard.id)}
                    deletePending={purgeCardMutation.isPending}
                  />
                )
              })}
            </Stack>
            <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
              <Pagination
                page={cardsPage}
                count={cardsTotalPages}
                onChange={(_, value) => setCardsPage(value)}
                disabled={archivedCardsQuery.isLoading || cardsTotalCount === 0}
              />
            </Box>
          </ArchiveListState>
        ) : null}
      </Stack>
    </Box>
  )
}
