import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Avatar from '@mui/material/Avatar'
import Card from '@mui/material/Card'
import IconButton from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemButton from '@mui/material/ListItemButton'
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
import StarIcon from '@mui/icons-material/Star'
import StarBorderIcon from '@mui/icons-material/StarBorder'
import StarOutlineIcon from '@mui/icons-material/StarOutline'
import WorkspacePremiumRoundedIcon from '@mui/icons-material/WorkspacePremiumRounded'
import { ConfirmDialog } from '@/components/common/ConfirmDialog'
import { useArchiveProject, useLeaveProject } from '@/features/projects/api/projects'
import { useToggleFavorite } from '@/features/favorites'
import { useAuthStore } from '@/features/auth'
import { projectStyle } from '@/features/projects/utils/projectStyle'
import { timeAgo } from '@/utils/format'
import type { Project } from '@/lib/types'

interface ProjectListViewProps {
  projects: Project[]
  favoriteIds: Set<string>
}

export function ProjectListView({ projects, favoriteIds }: ProjectListViewProps) {
  const navigate = useNavigate()
  const archiveProject = useArchiveProject()
  const leaveProject = useLeaveProject()
  const toggleFavoriteMutation = useToggleFavorite()
  const currentUserId = useAuthStore((state) => state.user?.id)
  const [menuAnchor, setMenuAnchor] = useState<{ el: HTMLElement; project: Project } | null>(null)
  const [confirmAction, setConfirmAction] = useState<{
    type: 'archive' | 'leave'
    project: Project
  } | null>(null)

  const handleConfirm = () => {
    if (!confirmAction) return
    if (confirmAction.type === 'archive') {
      archiveProject.mutate(confirmAction.project.id)
    } else {
      leaveProject.mutate(confirmAction.project.id)
    }
    setConfirmAction(null)
  }

  return (
    <>
      <Card variant="outlined">
        <List disablePadding>
          {projects.map((project, index) => {
            const config = projectStyle
            const isOwner = currentUserId === project.ownerId
            const memberCount = project.memberCount ?? 0
            const boardCount = project.boardCount ?? 0

            return (
              <ListItem
                key={project.id}
                disablePadding
                divider={index < projects.length - 1}
                secondaryAction={
                  <Stack direction="row" spacing={0} alignItems="center">
                    <IconButton
                      aria-label={
                        favoriteIds.has(project.id) ? 'Remove from favorites' : 'Add to favorites'
                      }
                      onClick={(e) => {
                        e.stopPropagation()
                        toggleFavoriteMutation.mutate({ entityType: 2, entityId: project.id })
                      }}
                      onMouseDown={(e) => e.stopPropagation()}
                      sx={{ display: { xs: 'none', sm: 'inline-flex' } }}
                    >
                      {favoriteIds.has(project.id) ? (
                        <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
                      ) : (
                        <StarBorderIcon fontSize="small" sx={{ color: 'text.disabled' }} />
                      )}
                    </IconButton>
                    <IconButton
                      edge="end"
                      aria-label="Project actions"
                      onClick={(e) => {
                        e.stopPropagation()
                        setMenuAnchor({ el: e.currentTarget, project })
                      }}
                      onMouseDown={(e) => e.stopPropagation()}
                    >
                      <MoreVertIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                }
              >
                <ListItemButton
                  onClick={() => navigate({ to: '/projects/$projectId', params: { projectId: project.id } })}
                  sx={{ py: { xs: 1, sm: 1.5 }, px: { xs: 1.5, sm: 2.5 }, pr: { xs: 7, sm: 12 } }}
                >
                  <Avatar
                    sx={{
                      width: { xs: 32, sm: 36 },
                      height: { xs: 32, sm: 36 },
                      bgcolor: config.bg,
                      color: config.color,
                      mr: { xs: 1.25, sm: 2 },
                      flexShrink: 0,
                    }}
                  >
                    <DashboardOutlinedIcon sx={{ fontSize: 18 }} />
                  </Avatar>

                  <ListItemText
                    primary={
                      <Stack direction="row" alignItems="center" spacing={0.5}>
                        <Typography variant="subtitle2" noWrap sx={{ fontWeight: 600 }}>
                          {project.name}
                        </Typography>
                        {isOwner ? (
                          <Tooltip title="You own this workspace">
                            <WorkspacePremiumRoundedIcon sx={{ fontSize: 14, color: 'text.secondary' }} />
                          </Tooltip>
                        ) : null}
                      </Stack>
                    }
                    secondary={
                      <>
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          component="span"
                          noWrap
                          sx={{ display: { xs: 'block', sm: 'none' }, mt: 0.25 }}
                        >
                          {memberCount} · {boardCount} {boardCount === 1 ? 'board' : 'boards'} · {timeAgo(project.updatedAt)}
                        </Typography>
                        <Stack
                          direction="row"
                          spacing={2}
                          component="span"
                          sx={{ mt: 0.25, display: { xs: 'none', sm: 'flex' } }}
                        >
                          <Typography variant="caption" color="text.secondary" component="span">
                            {memberCount} {memberCount === 1 ? 'member' : 'members'}
                          </Typography>
                          <Typography variant="caption" color="text.secondary" component="span">
                            {boardCount} {boardCount === 1 ? 'board' : 'boards'}
                          </Typography>
                          <Typography variant="caption" color="text.disabled" component="span">
                            {timeAgo(project.updatedAt)}
                          </Typography>
                        </Stack>
                      </>
                    }
                  />
                </ListItemButton>
              </ListItem>
            )
          })}
        </List>
      </Card>

      <Menu
        anchorEl={menuAnchor?.el}
        open={Boolean(menuAnchor)}
        onClose={() => setMenuAnchor(null)}
        slotProps={{ paper: { sx: { minWidth: 180 } } }}
      >
        {menuAnchor ? (
          <MenuItem
            onClick={() => {
              toggleFavoriteMutation.mutate({ entityType: 2, entityId: menuAnchor.project.id })
              setMenuAnchor(null)
            }}
          >
            <ListItemIcon>
              {favoriteIds.has(menuAnchor.project.id) ? (
                <StarIcon fontSize="small" sx={{ color: 'warning.main' }} />
              ) : (
                <StarOutlineIcon fontSize="small" />
              )}
            </ListItemIcon>
            <ListItemText>
              {favoriteIds.has(menuAnchor.project.id) ? 'Remove favorite' : 'Add favorite'}
            </ListItemText>
          </MenuItem>
        ) : null}
        {menuAnchor && currentUserId === menuAnchor.project.ownerId ? (
          <MenuItem
            onClick={() => {
              setConfirmAction({ type: 'archive', project: menuAnchor.project })
              setMenuAnchor(null)
            }}
          >
            <ListItemIcon>
              <ArchiveOutlinedIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>Archive</ListItemText>
          </MenuItem>
        ) : null}
        <MenuItem
          onClick={() => {
            if (menuAnchor) {
              setConfirmAction({ type: 'leave', project: menuAnchor.project })
            }
            setMenuAnchor(null)
          }}
        >
          <ListItemIcon>
            <LogoutIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Leave</ListItemText>
        </MenuItem>
      </Menu>

      {confirmAction ? (
        <ConfirmDialog
          open
          title={confirmAction.type === 'archive' ? 'Archive workspace' : 'Leave workspace'}
          description={
            confirmAction.type === 'archive'
              ? `Are you sure you want to archive "${confirmAction.project.name}"? You can restore it later from the archives.`
              : `Are you sure you want to leave "${confirmAction.project.name}"? You will lose access to all boards and tasks in this workspace.`
          }
          confirmLabel={confirmAction.type === 'archive' ? 'Archive' : 'Leave'}
          confirmColor={confirmAction.type === 'archive' ? 'warning' : 'error'}
          onConfirm={handleConfirm}
          onCancel={() => setConfirmAction(null)}
        />
      ) : null}
    </>
  )
}
