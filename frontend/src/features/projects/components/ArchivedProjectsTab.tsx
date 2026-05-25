import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemText from '@mui/material/ListItemText'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import SearchIcon from '@mui/icons-material/Search'
import { EmptyState } from '@/components/feedback/EmptyState'
import { useAuthStore } from '@/features/auth'
import {
  useArchivedProjects,
  usePurgeProject,
  useRestoreProject,
} from '@/features/projects/api/projects'
import { ProjectGallerySkeleton } from '@/features/projects/components/ProjectGallerySkeleton'
import { ProjectListSkeleton } from '@/features/projects/components/ProjectListSkeleton'
import { projectStyle } from '@/features/projects/utils/projectStyle'
import { timeAgo } from '@/utils/format'

interface ArchivedProjectsTabProps {
  viewMode: 'grid' | 'list'
  search: string
}

export function ArchivedProjectsTab({ viewMode, search }: ArchivedProjectsTabProps) {
  const archivedProjectsQuery = useArchivedProjects()
  const restoreProjectMutation = useRestoreProject()
  const purgeProjectMutation = usePurgeProject()
  const currentUserId = useAuthStore((state) => state.user?.id)

  const allArchivedProjects = archivedProjectsQuery.data?.items ?? []
  const trimmedQuery = search.trim().toLowerCase()
  const archivedProjects = trimmedQuery
    ? allArchivedProjects.filter((project) => project.name.toLowerCase().includes(trimmedQuery))
    : allArchivedProjects

  if (archivedProjectsQuery.isLoading) {
    return viewMode === 'grid' ? <ProjectGallerySkeleton /> : <ProjectListSkeleton />
  }

  if (allArchivedProjects.length === 0) {
    return (
      <Stack alignItems="center" spacing={1.5} sx={{ py: 8 }}>
        <ArchiveOutlinedIcon sx={{ fontSize: 48, color: 'text.disabled' }} />
        <Typography variant="body1" color="text.secondary">
          No archived workspaces
        </Typography>
      </Stack>
    )
  }

  if (archivedProjects.length === 0) {
    return <EmptyState icon={SearchIcon} title={`No archived workspaces matching “${search}”`} compact />
  }

  const config = projectStyle

  if (viewMode === 'list') {
    return (
      <Card variant="outlined" sx={{ opacity: 0.7 }}>
        <List disablePadding>
          {archivedProjects.map((project, index) => {
            const isOwner = Boolean(currentUserId) && project.ownerId === currentUserId
            return (
            <ListItem
              key={project.id}
              divider={index < archivedProjects.length - 1}
              sx={{ py: 1.5, px: 2.5 }}
              secondaryAction={
                <Stack direction="row" spacing={1}>
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => restoreProjectMutation.mutate(project.id)}
                    disabled={restoreProjectMutation.isPending || purgeProjectMutation.isPending}
                  >
                    Restore
                  </Button>
                  {isOwner ? (
                    <Button
                      size="small"
                      variant="outlined"
                      color="error"
                      onClick={() => purgeProjectMutation.mutate(project.id)}
                      disabled={restoreProjectMutation.isPending || purgeProjectMutation.isPending}
                    >
                      Delete permanently
                    </Button>
                  ) : null}
                </Stack>
              }
            >
              <Avatar
                sx={{
                  width: 36,
                  height: 36,
                  bgcolor: config.bg,
                  color: config.color,
                  mr: 2,
                  flexShrink: 0,
                }}
              >
                <DashboardOutlinedIcon sx={{ fontSize: 18 }} />
              </Avatar>
              <ListItemText
                primary={
                  <Typography variant="subtitle2" noWrap sx={{ fontWeight: 600 }}>
                    {project.name}
                  </Typography>
                }
                secondary={
                  <Typography variant="caption" color="text.disabled" component="span">
                    Archived {timeAgo(project.updatedAt)}
                  </Typography>
                }
              />
            </ListItem>
            )
          })}
        </List>
      </Card>
    )
  }

  return (
    <Grid container spacing={2.5}>
      {archivedProjects.map((project) => {
        const isOwner = Boolean(currentUserId) && project.ownerId === currentUserId
        return (
        <Grid key={project.id} size={{ xs: 12, sm: 6, lg: 4 }}>
          <Card variant="outlined" sx={{ opacity: 0.7 }}>
            <CardContent sx={{ p: 2.5 }}>
              <Stack spacing={2}>
                <Stack direction="row" alignItems="center" spacing={1.5}>
                  <Avatar
                    sx={{
                      width: 40,
                      height: 40,
                      bgcolor: config.bg,
                      color: config.color,
                    }}
                  >
                    <DashboardOutlinedIcon sx={{ fontSize: 20 }} />
                  </Avatar>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap>
                      {project.name}
                    </Typography>
                    <Typography variant="caption" color="text.disabled">
                      Archived {timeAgo(project.updatedAt)}
                    </Typography>
                  </Box>
                </Stack>
                <Stack direction="row" spacing={1} justifyContent="flex-end">
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => restoreProjectMutation.mutate(project.id)}
                    disabled={restoreProjectMutation.isPending || purgeProjectMutation.isPending}
                  >
                    Restore
                  </Button>
                  {isOwner ? (
                    <Button
                      size="small"
                      variant="outlined"
                      color="error"
                      onClick={() => purgeProjectMutation.mutate(project.id)}
                      disabled={restoreProjectMutation.isPending || purgeProjectMutation.isPending}
                    >
                      Delete permanently
                    </Button>
                  ) : null}
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
        )
      })}
    </Grid>
  )
}
