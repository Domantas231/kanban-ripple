import { useMemo, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Fab from '@mui/material/Fab'
import Grid from '@mui/material/Grid'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Pagination from '@mui/material/Pagination'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Tooltip from '@mui/material/Tooltip'
import AddIcon from '@mui/icons-material/Add'
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward'
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward'
import GridViewRoundedIcon from '@mui/icons-material/GridViewRounded'
import SearchIcon from '@mui/icons-material/Search'
import SortIcon from '@mui/icons-material/Sort'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import ViewKanbanOutlinedIcon from '@mui/icons-material/ViewKanbanOutlined'
import ViewListRoundedIcon from '@mui/icons-material/ViewListRounded'
import { EmptyState } from '@/components/feedback/EmptyState'
import { SegmentedControl } from '@/components/common/SegmentedControl'
import {
  useArchiveBoard,
  useArchivedBoards,
  useBoards,
} from '@/features/boards/api/boards'
import { ArchivedBoardsTab } from '@/features/boards/components/ArchivedBoardsTab'
import { BoardCard } from '@/features/boards/components/BoardCard'
import { BoardGridSkeleton } from '@/features/boards/components/BoardGridSkeleton'
import { BoardListView } from '@/features/boards/components/BoardListView'
import { CreateBoardDialog } from '@/features/boards/components/CreateBoardDialog'
import { TrelloImportDialog } from '@/features/boards/components/TrelloImportDialog'
import { ProjectHeader } from '@/features/projects'
import { useFavorites, useToggleFavorite } from '@/features/favorites'
import { useProject, useProjectMembers } from '@/features/projects'
import { useUiStore } from '@/stores/uiStore'
import type { Board } from '@/lib/types'

type BoardSortKey = 'name' | 'updated' | 'cards'

const BOARD_PAGE_SIZE = 9

interface ProjectDashboardProps {
  projectId: string
  canManageBoards: boolean
}

export function ProjectDashboard({ projectId, canManageBoards }: ProjectDashboardProps) {
  const navigate = useNavigate()
  const enqueueToast = useUiStore((state) => state.enqueueToast)
  const projectQuery = useProject(projectId)
  const boardsQuery = useBoards(projectId)
  const archivedBoardsQuery = useArchivedBoards()

  const membersQuery = useProjectMembers(projectId)
  const archiveBoardMutation = useArchiveBoard()
  const favoritesQuery = useFavorites()
  const toggleFavoriteMutation = useToggleFavorite()
  const favoriteBoardIds = useMemo(() => {
    const set = new Set<string>()
    for (const fav of favoritesQuery.data ?? []) {
      if (fav.entityType === 3) set.add(fav.entityId)
    }
    return set
  }, [favoritesQuery.data])

  const [createDialogOpen, setCreateDialogOpen] = useState(false)
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const [boardsTab, setBoardsTab] = useState(0)
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid')
  const [archiveTarget, setArchiveTarget] = useState<Board | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [sortKey, setSortKey] = useState<BoardSortKey>('updated')
  const [sortAsc, setSortAsc] = useState(false)
  const [sortMenuAnchor, setSortMenuAnchor] = useState<HTMLElement | null>(null)
  const [fabMenuAnchor, setFabMenuAnchor] = useState<HTMLElement | null>(null)

  const boards = useMemo(() => boardsQuery.data ?? [], [boardsQuery.data])

  const archivedCount = useMemo(
    () => (archivedBoardsQuery.data ?? []).filter((b: Board) => b.projectId === projectId).length,
    [archivedBoardsQuery.data, projectId],
  )

  const filteredAndSortedBoards = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase()
    const filtered = normalizedQuery
      ? boards.filter((b) => b.name.toLowerCase().includes(normalizedQuery))
      : [...boards]

    return filtered.sort((a, b) => {
      let cmp = 0
      if (sortKey === 'name') {
        cmp = a.name.localeCompare(b.name)
      } else if (sortKey === 'updated') {
        cmp = new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
      } else if (sortKey === 'cards') {
        cmp = (b.cardCount ?? 0) - (a.cardCount ?? 0)
      }
      return sortAsc ? -cmp : cmp
    })
  }, [boards, searchQuery, sortKey, sortAsc])

  const favoriteBoardCount = useMemo(
    () => boards.filter((b) => favoriteBoardIds.has(b.id)).length,
    [boards, favoriteBoardIds],
  )

  const favoriteFilteredBoards = useMemo(
    () => filteredAndSortedBoards.filter((b) => favoriteBoardIds.has(b.id)),
    [filteredAndSortedBoards, favoriteBoardIds],
  )

  const [boardPage, setBoardPage] = useState(1)
  const [favBoardPage, setFavBoardPage] = useState(1)
  const [prevPagingDeps, setPrevPagingDeps] = useState({ searchQuery, sortKey, sortAsc })

  if (
    prevPagingDeps.searchQuery !== searchQuery ||
    prevPagingDeps.sortKey !== sortKey ||
    prevPagingDeps.sortAsc !== sortAsc
  ) {
    setPrevPagingDeps({ searchQuery, sortKey, sortAsc })
    setBoardPage(1)
    setFavBoardPage(1)
  }

  const boardTotalPages = Math.ceil(filteredAndSortedBoards.length / BOARD_PAGE_SIZE)
  const paginatedBoards = filteredAndSortedBoards.slice(
    (boardPage - 1) * BOARD_PAGE_SIZE,
    boardPage * BOARD_PAGE_SIZE,
  )

  const favBoardTotalPages = Math.ceil(favoriteFilteredBoards.length / BOARD_PAGE_SIZE)
  const paginatedFavBoards = favoriteFilteredBoards.slice(
    (favBoardPage - 1) * BOARD_PAGE_SIZE,
    favBoardPage * BOARD_PAGE_SIZE,
  )

  const handleArchiveBoard = async () => {
    if (!archiveTarget || archiveBoardMutation.isPending) return
    try {
      await archiveBoardMutation.mutateAsync(archiveTarget.id)
      enqueueToast({ message: `"${archiveTarget.name}" archived`, severity: 'success' })
    } catch {
      enqueueToast({ message: 'Failed to archive board', severity: 'error' })
    }
    setArchiveTarget(null)
  }

  const handleSortChange = (key: BoardSortKey) => {
    if (sortKey === key) {
      setSortAsc((prev) => !prev)
    } else {
      setSortKey(key)
      setSortAsc(false)
    }
    setSortMenuAnchor(null)
  }

  const memberCount = membersQuery.data?.length ?? projectQuery.data?.memberCount ?? 0

  return (
    <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
      <Stack spacing={4}>
        {projectQuery.data ? (
          <ProjectHeader
            projectId={projectId}
            name={projectQuery.data.name}
            memberCount={memberCount}
            canEdit={canManageBoards}
          />
        ) : (
          <Skeleton variant="text" width={300} height={40} />
        )}

        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ md: 'center' }}
          spacing={{ xs: 1.5, md: 2 }}
        >
            <SegmentedControl
              value={boardsTab}
              onChange={setBoardsTab}
              options={[
                { label: 'All', count: boards.length },
                { label: 'Favorites', count: favoriteBoardCount },
                { label: 'Archived', count: archivedCount },
              ]}
            />

            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              sx={{ width: { xs: '100%', md: 'auto' } }}
            >
              <TextField
                size="small"
                placeholder="Search boards..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                slotProps={{
                  input: {
                    startAdornment: (
                      <InputAdornment position="start">
                        <SearchIcon sx={{ fontSize: 18, color: 'text.disabled' }} />
                      </InputAdornment>
                    ),
                  },
                }}
                sx={{ flex: { xs: 1, md: '0 0 auto' }, width: { md: 200 } }}
              />

              <Tooltip title="Sort boards">
                <IconButton
                  onClick={(e) => setSortMenuAnchor(e.currentTarget)}
                  aria-label="Sort boards"
                  sx={{ minWidth: 44, minHeight: 44 }}
                >
                  <SortIcon sx={{ fontSize: 20 }} />
                </IconButton>
              </Tooltip>
              <Menu
                anchorEl={sortMenuAnchor}
                open={Boolean(sortMenuAnchor)}
                onClose={() => setSortMenuAnchor(null)}
                slotProps={{ paper: { sx: { minWidth: 180 } } }}
              >
                {[
                  { key: 'updated' as const, label: 'Last updated' },
                  { key: 'name' as const, label: 'Name' },
                  { key: 'cards' as const, label: 'Card count' },
                ].map((opt) => (
                  <MenuItem key={opt.key} onClick={() => handleSortChange(opt.key)} selected={sortKey === opt.key}>
                    <ListItemText>{opt.label}</ListItemText>
                    {sortKey === opt.key ? (
                      sortAsc ? (
                        <ArrowUpwardIcon sx={{ fontSize: 16, ml: 1 }} />
                      ) : (
                        <ArrowDownwardIcon sx={{ fontSize: 16, ml: 1 }} />
                      )
                    ) : null}
                  </MenuItem>
                ))}
              </Menu>

              <ToggleButtonGroup
                value={viewMode}
                exclusive
                onChange={(_, value) => {
                  if (value) setViewMode(value)
                }}
                size="small"
                sx={{
                  '& .MuiToggleButton-root': {
                    minWidth: 44,
                    minHeight: 44,
                  },
                }}
              >
                <ToggleButton value="grid" aria-label="Grid view">
                  <GridViewRoundedIcon fontSize="small" />
                </ToggleButton>
                <ToggleButton value="list" aria-label="List view">
                  <ViewListRoundedIcon fontSize="small" />
                </ToggleButton>
              </ToggleButtonGroup>

              {canManageBoards ? (
                <>
                  <Button
                    variant="outlined"
                    size="small"
                    startIcon={<UploadFileIcon />}
                    onClick={() => setImportDialogOpen(true)}
                    sx={{
                      whiteSpace: 'nowrap',
                      display: { xs: 'none', lg: 'inline-flex' },
                    }}
                  >
                    Import Trello
                  </Button>
                  <Button
                    variant="contained"
                    size="small"
                    startIcon={<AddIcon />}
                    onClick={() => setCreateDialogOpen(true)}
                    sx={{
                      whiteSpace: 'nowrap',
                      display: { xs: 'none', lg: 'inline-flex' },
                    }}
                  >
                    New Board
                  </Button>
                </>
              ) : null}
            </Stack>
          </Stack>

        {boardsQuery.isError ? <Alert severity="error">Unable to load boards.</Alert> : null}

        {boardsQuery.isLoading ? (
          <BoardGridSkeleton />
        ) : boardsTab === 0 ? (
          <>
            {boards.length === 0 ? (
              <EmptyState
                icon={ViewKanbanOutlinedIcon}
                title="No boards yet"
                description="Boards organize your cards into columns. Create your first board to get started."
                actionLabel="Create Board"
                actionIcon={<AddIcon />}
                onAction={() => setCreateDialogOpen(true)}
              />
            ) : filteredAndSortedBoards.length === 0 && searchQuery.trim() ? (
              <EmptyState
                icon={SearchIcon}
                title={`No boards matching “${searchQuery.trim()}”`}
                compact
              />
            ) : viewMode === 'grid' ? (
              <>
                <Grid container spacing={2.5}>
                  {paginatedBoards.map((board) => (
                    <Grid key={board.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                      <BoardCard
                        board={board}
                        cardCount={board.cardCount ?? 0}
                        columnCount={board.columnCount ?? 0}
                        canManage={canManageBoards}
                        isFavorite={favoriteBoardIds.has(board.id)}
                        onToggleFavorite={() =>
                          toggleFavoriteMutation.mutate({ entityType: 3, entityId: board.id })
                        }
                        onClick={() =>
                          navigate({
                            to: '/projects/$projectId/boards/$boardId',
                            params: { projectId, boardId: board.id },
                          })
                        }
                        onArchive={() => setArchiveTarget(board)}
                      />
                    </Grid>
                  ))}
                </Grid>
                {boardTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={boardTotalPages} page={boardPage} onChange={(_, v) => setBoardPage(v)} />
                  </Box>
                ) : null}
              </>
            ) : (
              <>
                <BoardListView
                  boards={paginatedBoards}
                  canManage={canManageBoards}
                  projectId={projectId}
                  onArchive={setArchiveTarget}
                  favoriteBoardIds={favoriteBoardIds}
                  onToggleFavorite={(boardId) =>
                    toggleFavoriteMutation.mutate({ entityType: 3, entityId: boardId })
                  }
                />
                {boardTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={boardTotalPages} page={boardPage} onChange={(_, v) => setBoardPage(v)} />
                  </Box>
                ) : null}
              </>
            )}
          </>
        ) : boardsTab === 1 ? (
          <>
            {favoriteFilteredBoards.length === 0 ? (
              <EmptyState
                icon={StarBorderIcon}
                title="No favorite boards"
                description="Star a board to quickly find it here."
                compact
              />
            ) : viewMode === 'grid' ? (
              <>
                <Grid container spacing={2.5}>
                  {paginatedFavBoards.map((board) => (
                    <Grid key={board.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                      <BoardCard
                        board={board}
                        cardCount={board.cardCount ?? 0}
                        columnCount={board.columnCount ?? 0}
                        canManage={canManageBoards}
                        isFavorite
                        onToggleFavorite={() =>
                          toggleFavoriteMutation.mutate({ entityType: 3, entityId: board.id })
                        }
                        onClick={() =>
                          navigate({
                            to: '/projects/$projectId/boards/$boardId',
                            params: { projectId, boardId: board.id },
                          })
                        }
                        onArchive={() => setArchiveTarget(board)}
                      />
                    </Grid>
                  ))}
                </Grid>
                {favBoardTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination
                      count={favBoardTotalPages}
                      page={favBoardPage}
                      onChange={(_, v) => setFavBoardPage(v)}
                    />
                  </Box>
                ) : null}
              </>
            ) : (
              <>
                <BoardListView
                  boards={paginatedFavBoards}
                  canManage={canManageBoards}
                  projectId={projectId}
                  onArchive={setArchiveTarget}
                  favoriteBoardIds={favoriteBoardIds}
                  onToggleFavorite={(boardId) =>
                    toggleFavoriteMutation.mutate({ entityType: 3, entityId: boardId })
                  }
                />
                {favBoardTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination
                      count={favBoardTotalPages}
                      page={favBoardPage}
                      onChange={(_, v) => setFavBoardPage(v)}
                    />
                  </Box>
                ) : null}
              </>
            )}
          </>
        ) : (
          <ArchivedBoardsTab
            projectId={projectId}
            viewMode={viewMode}
            search={searchQuery}
            canManage={canManageBoards}
          />
        )}
      </Stack>

      <Dialog
        open={Boolean(archiveTarget)}
        onClose={() => {
          if (!archiveBoardMutation.isPending) setArchiveTarget(null)
        }}
        fullWidth
        maxWidth="xs"
        aria-labelledby="archive-board-title"
      >
        <DialogTitle id="archive-board-title">Archive Board</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to archive{archiveTarget ? ` "${archiveTarget.name}"` : ''}? You can restore it later from the Archived tab.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setArchiveTarget(null)} disabled={archiveBoardMutation.isPending}>
            Cancel
          </Button>
          <Button
            onClick={handleArchiveBoard}
            color="warning"
            variant="contained"
            disabled={archiveBoardMutation.isPending}
          >
            {archiveBoardMutation.isPending ? 'Archiving...' : 'Archive'}
          </Button>
        </DialogActions>
      </Dialog>

      <CreateBoardDialog
        open={createDialogOpen}
        onClose={() => setCreateDialogOpen(false)}
        projectId={projectId}
      />
      <TrelloImportDialog
        open={importDialogOpen}
        onClose={() => setImportDialogOpen(false)}
        projectId={projectId}
      />

      {canManageBoards ? (
        <>
          <Fab
            color="primary"
            aria-label="Create board options"
            aria-haspopup="menu"
            aria-expanded={Boolean(fabMenuAnchor)}
            onClick={(e) => setFabMenuAnchor(e.currentTarget)}
            sx={{
              display:
                createDialogOpen || importDialogOpen || Boolean(archiveTarget)
                  ? 'none'
                  : { xs: 'inline-flex', lg: 'none' },
              position: 'fixed',
              right: 16,
              bottom: 'calc(16px + env(safe-area-inset-bottom))',
              zIndex: (theme) => theme.zIndex.fab,
            }}
          >
            <AddIcon />
          </Fab>
          <Menu
            anchorEl={fabMenuAnchor}
            open={Boolean(fabMenuAnchor)}
            onClose={() => setFabMenuAnchor(null)}
            anchorOrigin={{ vertical: 'top', horizontal: 'right' }}
            transformOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            slotProps={{ paper: { sx: { minWidth: 200, mb: 1 } } }}
          >
            <MenuItem
              onClick={() => {
                setFabMenuAnchor(null)
                setCreateDialogOpen(true)
              }}
            >
              <ListItemIcon>
                <AddIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>New Board</ListItemText>
            </MenuItem>
            <MenuItem
              onClick={() => {
                setFabMenuAnchor(null)
                setImportDialogOpen(true)
              }}
            >
              <ListItemIcon>
                <UploadFileIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Import from Trello</ListItemText>
            </MenuItem>
          </Menu>
        </>
      ) : null}
    </Box>
  )
}
