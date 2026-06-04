import { useMemo, useState } from 'react'
import { useNavigate, useRouterState } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Fab from '@mui/material/Fab'
import Grid from '@mui/material/Grid'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Pagination from '@mui/material/Pagination'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward'
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward'
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined'
import GridViewRoundedIcon from '@mui/icons-material/GridViewRounded'
import SearchIcon from '@mui/icons-material/Search'
import SortIcon from '@mui/icons-material/Sort'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import ViewListRoundedIcon from '@mui/icons-material/ViewListRounded'
import { EmptyState } from '@/components/feedback/EmptyState'
import { SegmentedControl } from '@/components/common/SegmentedControl'
import { CreateProjectDialog } from '@/features/projects/components/CreateProjectDialog'
import { ArchivedProjectsTab } from '@/features/projects/components/ArchivedProjectsTab'
import { ProjectCard } from '@/features/projects/components/ProjectCard'
import { ProjectGallerySkeleton } from '@/features/projects/components/ProjectGallerySkeleton'
import { ProjectListSkeleton } from '@/features/projects/components/ProjectListSkeleton'
import { ProjectListView } from '@/features/projects/components/ProjectListView'
import { useArchivedProjects, useProjects } from '@/features/projects/api/projects'
import { useFavorites } from '@/features/favorites'

type ViewMode = 'grid' | 'list'
type ProjectSortKey = 'name' | 'updated' | 'boards'

const PAGE_SIZE = 9

export function ProjectsListPage() {
  const searchParams = useRouterState({
    select: (state) => state.location.search as Record<string, string>,
  })
  const navigate = useNavigate()

  const search = searchParams.q ?? ''
  const viewMode = (searchParams.view as ViewMode) ?? 'grid'

  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [tab, setTab] = useState(0)
  const [sortKey, setSortKey] = useState<ProjectSortKey>('updated')
  const [sortAsc, setSortAsc] = useState(false)
  const [sortMenuAnchor, setSortMenuAnchor] = useState<HTMLElement | null>(null)
  const projectsQuery = useProjects()
  const archivedProjectsQuery = useArchivedProjects()
  const favoritesQuery = useFavorites()
  const favoriteEntityIds = useMemo(() => {
    const set = new Set<string>()
    for (const fav of favoritesQuery.data ?? []) {
      if (fav.entityType === 2) set.add(fav.entityId)
    }
    return set
  }, [favoritesQuery.data])

  const setSearch = (value: string) => {
    navigate({
      to: '/projects',
      search: (prev: Record<string, string>) => {
        const next: Record<string, string | undefined> = { ...prev, q: value }
        if (!value) delete next.q
        return next
      },
      replace: true,
    })
  }

  const setViewMode = (value: ViewMode) => {
    navigate({
      to: '/projects',
      search: (prev: Record<string, string>) => ({ ...prev, view: value }),
      replace: true,
    })
  }

  const handleSortChange = (key: ProjectSortKey) => {
    if (sortKey === key) {
      setSortAsc((prev) => !prev)
    } else {
      setSortKey(key)
      setSortAsc(false)
    }
    setSortMenuAnchor(null)
  }

  const filteredProjects = useMemo(() => {
    const projects = projectsQuery.data?.items ?? []
    const query = search.trim().toLowerCase()
    const filtered = query
      ? projects.filter((project) => project.name.toLowerCase().includes(query))
      : [...projects]

    return filtered.sort((a, b) => {
      let cmp = 0
      if (sortKey === 'name') {
        cmp = a.name.localeCompare(b.name)
      } else if (sortKey === 'updated') {
        cmp = new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
      } else if (sortKey === 'boards') {
        cmp = (b.boardCount ?? 0) - (a.boardCount ?? 0)
      }
      return sortAsc ? -cmp : cmp
    })
  }, [projectsQuery.data?.items, search, sortKey, sortAsc])

  const activeCount = (projectsQuery.data?.items ?? []).length
  const favoriteCount = favoriteEntityIds.size
  const archivedCount = (archivedProjectsQuery.data?.items ?? []).length
  const hasProjects = activeCount > 0
  const noSearchResults = hasProjects && filteredProjects.length === 0

  const favoriteProjects = useMemo(
    () => filteredProjects.filter((p) => favoriteEntityIds.has(p.id)),
    [filteredProjects, favoriteEntityIds],
  )

  const [page, setPage] = useState(1)
  const [favPage, setFavPage] = useState(1)
  const [prevPagingDeps, setPrevPagingDeps] = useState({ search, sortKey, sortAsc })

  if (
    prevPagingDeps.search !== search ||
    prevPagingDeps.sortKey !== sortKey ||
    prevPagingDeps.sortAsc !== sortAsc
  ) {
    setPrevPagingDeps({ search, sortKey, sortAsc })
    setPage(1)
    setFavPage(1)
  }

  const totalPages = Math.ceil(filteredProjects.length / PAGE_SIZE)
  const paginatedProjects = filteredProjects.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  const favTotalPages = Math.ceil(favoriteProjects.length / PAGE_SIZE)
  const paginatedFavorites = favoriteProjects.slice((favPage - 1) * PAGE_SIZE, favPage * PAGE_SIZE)

  return (
    <Box
      sx={{
        px: { xs: 1.5, sm: 2, md: 3 },
        pb: { xs: 'calc(env(safe-area-inset-bottom, 0px) + 36px)', sm: 2, md: 3 },
        pt: { xs: 1.5, sm: 2, md: 3 },
      }}
    >
      <Stack spacing={4}>
        <Box>
          <Typography variant="h3" sx={{ fontWeight: 700 }}>
            Workspaces
          </Typography>
        </Box>

        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ md: 'center' }}
          spacing={2}
        >
          <SegmentedControl
            value={tab}
            onChange={setTab}
            options={[
              { label: 'All', count: activeCount },
              { label: 'Favorites', count: favoriteCount },
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
              placeholder="Search workspaces..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
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

            <Tooltip title="Sort workspaces">
              <IconButton
                size="small"
                onClick={(e) => setSortMenuAnchor(e.currentTarget)}
                aria-label="Sort workspaces"
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
                { key: 'boards' as const, label: 'Board count' },
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
            >
              <ToggleButton value="grid" aria-label="Grid view">
                <GridViewRoundedIcon fontSize="small" />
              </ToggleButton>
              <ToggleButton value="list" aria-label="List view">
                <ViewListRoundedIcon fontSize="small" />
              </ToggleButton>
            </ToggleButtonGroup>

            <Button
              variant="contained"
              size="small"
              startIcon={<AddIcon />}
              onClick={() => setIsCreateOpen(true)}
              sx={{ whiteSpace: 'nowrap', display: { xs: 'none', lg: 'inline-flex' } }}
            >
              New Workspace
            </Button>
          </Stack>
        </Stack>

        {projectsQuery.isError ? <Alert severity="error">Unable to load workspaces.</Alert> : null}

        {tab === 0 ? (
          <>
            {projectsQuery.isLoading ? (
              viewMode === 'grid' ? <ProjectGallerySkeleton /> : <ProjectListSkeleton />
            ) : null}

            {!projectsQuery.isLoading && !hasProjects && !projectsQuery.isError ? (
              <EmptyState
                icon={FolderOutlinedIcon}
                title="No workspaces yet"
                description="Workspaces organize your boards and team. Create your first workspace to start collaborating."
                actionLabel="Create Workspace"
                actionIcon={<AddIcon />}
                onAction={() => setIsCreateOpen(true)}
              />
            ) : null}

            {!projectsQuery.isLoading && noSearchResults ? (
              <EmptyState
                icon={SearchIcon}
                title={`No workspaces matching “${search}”`}
                compact
              />
            ) : null}

            {!projectsQuery.isLoading && filteredProjects.length > 0 && viewMode === 'grid' ? (
              <>
                <Grid container spacing={2.5}>
                  {paginatedProjects.map((project) => (
                    <Grid key={project.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                      <ProjectCard project={project} isFavorite={favoriteEntityIds.has(project.id)} />
                    </Grid>
                  ))}
                </Grid>
                {totalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={totalPages} page={page} onChange={(_, v) => setPage(v)} />
                  </Box>
                ) : null}
              </>
            ) : null}

            {!projectsQuery.isLoading && filteredProjects.length > 0 && viewMode === 'list' ? (
              <>
                <ProjectListView projects={paginatedProjects} favoriteIds={favoriteEntityIds} />
                {totalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={totalPages} page={page} onChange={(_, v) => setPage(v)} />
                  </Box>
                ) : null}
              </>
            ) : null}
          </>
        ) : tab === 1 ? (
          <>
            {projectsQuery.isLoading ? (
              viewMode === 'grid' ? <ProjectGallerySkeleton /> : <ProjectListSkeleton />
            ) : favoriteProjects.length === 0 ? (
              <EmptyState
                icon={StarBorderIcon}
                title="No favorite workspaces"
                description="Star a workspace to quickly find it here."
                compact
              />
            ) : viewMode === 'grid' ? (
              <>
                <Grid container spacing={2.5}>
                  {paginatedFavorites.map((project) => (
                    <Grid key={project.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                      <ProjectCard project={project} isFavorite />
                    </Grid>
                  ))}
                </Grid>
                {favTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={favTotalPages} page={favPage} onChange={(_, v) => setFavPage(v)} />
                  </Box>
                ) : null}
              </>
            ) : (
              <>
                <ProjectListView projects={paginatedFavorites} favoriteIds={favoriteEntityIds} />
                {favTotalPages > 1 ? (
                  <Box sx={{ display: 'flex', justifyContent: 'center', mt: 3 }}>
                    <Pagination count={favTotalPages} page={favPage} onChange={(_, v) => setFavPage(v)} />
                  </Box>
                ) : null}
              </>
            )}
          </>
        ) : (
          <ArchivedProjectsTab viewMode={viewMode} search={search} />
        )}
      </Stack>

      <CreateProjectDialog open={isCreateOpen} onClose={() => setIsCreateOpen(false)} onCreated={() => {}} />

      <Fab
        color="primary"
        aria-label="New workspace"
        onClick={() => setIsCreateOpen(true)}
        sx={{
          display: isCreateOpen
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
    </Box>
  )
}
