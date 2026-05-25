import { useState, type MouseEvent } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import CardActionArea from '@mui/material/CardActionArea'
import CardContent from '@mui/material/CardContent'
import IconButton from '@mui/material/IconButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import ArchiveOutlinedIcon from '@mui/icons-material/ArchiveOutlined'
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined'
import LogoutIcon from '@mui/icons-material/Logout'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import PeopleOutlineIcon from '@mui/icons-material/PeopleOutline'
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import WorkspacePremiumRoundedIcon from '@mui/icons-material/WorkspacePremiumRounded'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { useArchiveProject, useLeaveProject } from '@/features/projects/api/projects'
import { useToggleFavorite } from '@/features/favorites'
import { useAuthStore } from '@/features/auth'
import { projectStyle } from '@/features/projects/utils/projectStyle'
import { timeAgo } from '@/utils/format'
import type { Project } from '@/lib/types'

interface ProjectCardProps {
  project: Project
  isFavorite?: boolean
}

export function ProjectCard({ project, isFavorite = false }: ProjectCardProps) {
  const navigate = useNavigate()
  const archiveProject = useArchiveProject()
  const leaveProject = useLeaveProject()
  const toggleFavoriteMutation = useToggleFavorite()
  const currentUserId = useAuthStore((state) => state.user?.id)
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const [confirmAction, setConfirmAction] = useState<'archive' | 'leave' | null>(null)

  const isOwner = currentUserId === project.ownerId

  const handleMenuOpen = (event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation()
    setMenuAnchor(event.currentTarget)
  }

  const handleMenuClose = () => setMenuAnchor(null)

  const handleOpen = () => {
    navigate({ to: '/projects/$projectId', params: { projectId: project.id } })
  }

  const handleArchive = () => {
    handleMenuClose()
    setConfirmAction('archive')
  }

  const handleLeave = () => {
    handleMenuClose()
    setConfirmAction('leave')
  }

  const handleConfirm = () => {
    if (confirmAction === 'archive') {
      archiveProject.mutate(project.id)
    } else if (confirmAction === 'leave') {
      leaveProject.mutate(project.id)
    }
    setConfirmAction(null)
  }

  const config = projectStyle
  const memberCount = project.memberCount ?? 0
  const boardCount = project.boardCount ?? 0

  return (
    <>
      <Card
        variant="outlined"
        sx={{
          height: '100%',
          position: 'relative',
          overflow: 'hidden',
          transition: 'box-shadow 150ms ease, border-color 150ms ease',
          '&:hover': {
            boxShadow: 2,
            borderColor: 'primary.main',
          },
        }}
      >
        <Box sx={{ height: 6, bgcolor: config.color }} />

        <CardActionArea onClick={handleOpen} sx={{ height: 'calc(100% - 6px)' }}>
          <CardContent sx={{ p: 2.5, height: '100%' }}>
            <Stack spacing={2} sx={{ height: '100%' }}>
              <Stack direction="row" alignItems="center" spacing={1.5}>
                <Avatar
                  sx={{
                    width: 40,
                    height: 40,
                    bgcolor: config.bg,
                    color: config.color,
                    flexShrink: 0,
                  }}
                >
                  <DashboardOutlinedIcon sx={{ fontSize: 20 }} />
                </Avatar>

                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack direction="row" alignItems="center" spacing={0.5}>
                    <Typography variant="subtitle1" sx={{ fontWeight: 600, lineHeight: 1.3 }} noWrap>
                      {project.name}
                    </Typography>
                    {isOwner ? (
                      <Tooltip title="You own this workspace">
                        <WorkspacePremiumRoundedIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                      </Tooltip>
                    ) : null}
                  </Stack>
                </Box>

                <Stack direction="row" spacing={0} sx={{ flexShrink: 0, mt: -0.5, mr: -0.5 }}>
                  <IconButton
                    aria-label={isFavorite ? 'Remove from favorites' : 'Add to favorites'}
                    onClick={(e) => {
                      e.stopPropagation()
                      toggleFavoriteMutation.mutate({ entityType: 2, entityId: project.id })
                    }}
                    onMouseDown={(e) => e.stopPropagation()}
                    sx={{ p: { xs: 1, sm: 0.75 } }}
                  >
                    {isFavorite ? (
                      <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
                    ) : (
                      <StarBorderIcon fontSize="small" sx={{ color: 'text.disabled' }} />
                    )}
                  </IconButton>
                  <IconButton
                    aria-label="Project actions"
                    onClick={handleMenuOpen}
                    onMouseDown={(e) => e.stopPropagation()}
                    sx={{ p: { xs: 1, sm: 0.75 } }}
                  >
                    <MoreVertIcon fontSize="small" />
                  </IconButton>
                </Stack>
              </Stack>

              <Stack
                direction="row"
                justifyContent="space-between"
                alignItems="center"
                sx={{ mt: 'auto' }}
              >
                <Stack direction="row" spacing={2} alignItems="center">
                  <Stack direction="row" spacing={0.5} alignItems="center">
                    <PeopleOutlineIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                    <Typography variant="caption" color="text.secondary">
                      {memberCount}
                    </Typography>
                  </Stack>
                  <Stack direction="row" spacing={0.5} alignItems="center">
                    <DashboardOutlinedIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                    <Typography variant="caption" color="text.secondary">
                      {boardCount}
                    </Typography>
                  </Stack>
                </Stack>

                <Typography variant="caption" color="text.disabled">
                  {timeAgo(project.updatedAt)}
                </Typography>
              </Stack>
            </Stack>
          </CardContent>
        </CardActionArea>

        <Menu
          anchorEl={menuAnchor}
          open={Boolean(menuAnchor)}
          onClose={handleMenuClose}
          onClick={(e) => e.stopPropagation()}
          slotProps={{ paper: { sx: { minWidth: 160 } } }}
        >
          {isOwner ? (
            <MenuItem onClick={handleArchive}>
              <ListItemIcon>
                <ArchiveOutlinedIcon fontSize="small" />
              </ListItemIcon>
              <ListItemText>Archive</ListItemText>
            </MenuItem>
          ) : null}
          <MenuItem onClick={handleLeave}>
            <ListItemIcon>
              <LogoutIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Leave</ListItemText>
          </MenuItem>
        </Menu>
      </Card>

      <ConfirmDialog
        open={confirmAction === 'archive'}
        title="Archive workspace"
        description={`Are you sure you want to archive "${project.name}"? You can restore it later from the archives.`}
        confirmLabel="Archive"
        confirmColor="warning"
        onConfirm={handleConfirm}
        onCancel={() => setConfirmAction(null)}
      />
      <ConfirmDialog
        open={confirmAction === 'leave'}
        title="Leave workspace"
        description={`Are you sure you want to leave "${project.name}"? You will lose access to all boards and tasks in this workspace.`}
        confirmLabel="Leave"
        onConfirm={handleConfirm}
        onCancel={() => setConfirmAction(null)}
      />
    </>
  )
}
