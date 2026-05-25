import { useMemo, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Select from '@mui/material/Select'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import EmailOutlinedIcon from '@mui/icons-material/EmailOutlined'
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import PeopleOutlineIcon from '@mui/icons-material/PeopleOutline'
import PersonRemoveOutlinedIcon from '@mui/icons-material/PersonRemoveOutlined'
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined'
import { UserAvatar } from '@/features/auth'
import { EmptyState } from '@/components/feedback/EmptyState'
import {
  useArchiveProject,
  useInviteUser,
  useProject,
  useProjectMembers,
  useRemoveMember,
  useTransferOwnership,
  useUpdateMemberRole,
  useUpdateProject,
} from '@/features/projects/api/projects'
import { useAuthStore } from '@/features/auth'
import type { Guid, ProjectMember, ProjectRole } from '@/lib/types'
import { isValidEmail, projectRoleLabel } from '@/features/projects/utils/projectHelpers'

interface ProjectSettingsPageProps {
  projectId: string
}

export function ProjectSettingsPage({ projectId }: ProjectSettingsPageProps) {
  const navigate = useNavigate()
  const currentUserId = useAuthStore((state) => state.user?.id)

  const projectQuery = useProject(projectId)
  const membersQuery = useProjectMembers(projectId)
  const updateProjectMutation = useUpdateProject()
  const archiveProjectMutation = useArchiveProject()
  const inviteUserMutation = useInviteUser()
  const updateMemberRoleMutation = useUpdateMemberRole()
  const removeMemberMutation = useRemoveMember()
  const transferOwnershipMutation = useTransferOwnership()

  const project = projectQuery.data
  const members = membersQuery.data ?? []

  const currentUserRole = useMemo(() => {
    if (!currentUserId) return undefined
    if (project?.ownerId === currentUserId) return 0 as ProjectRole
    return members.find((m) => m.userId === currentUserId)?.role
  }, [currentUserId, members, project?.ownerId])

  const isOwner = currentUserRole === 0
  const canManageMembers = currentUserRole === 0 || currentUserRole === 1

  const [name, setName] = useState('')
  const [prevProjectName, setPrevProjectName] = useState<string | undefined>(project?.name)
  const [isArchiveDialogOpen, setIsArchiveDialogOpen] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteRole, setInviteRole] = useState<string>('2')
  const [memberToRemove, setMemberToRemove] = useState<ProjectMember | null>(null)
  const [memberToTransferTo, setMemberToTransferTo] = useState<ProjectMember | null>(null)

  if (project && project.name !== prevProjectName) {
    setPrevProjectName(project.name)
    setName(project.name)
  }

  const trimmedName = name.trim()
  const hasNameChanged = Boolean(project) && trimmedName.length > 0 && trimmedName !== project?.name
  const canSave = isOwner && hasNameChanged && !updateProjectMutation.isPending

  const handleSave = async () => {
    if (!project || !canSave) return
    await updateProjectMutation.mutateAsync({ id: project.id, data: { name: trimmedName } })
  }

  const handleInvite = async () => {
    if (!canManageMembers || !isValidEmail(inviteEmail) || inviteUserMutation.isPending) return
    try {
      await inviteUserMutation.mutateAsync({
        projectId,
        data: { email: inviteEmail.trim(), role: Number(inviteRole) as ProjectRole },
      })
    } catch {
      // The Alert below renders via inviteUserMutation.isError; swallow here
      // to avoid an unhandled promise rejection.
      return
    }
    setInviteEmail('')
  }

  const handleArchiveProject = async () => {
    if (!project || !isOwner || archiveProjectMutation.isPending) return
    await archiveProjectMutation.mutateAsync(project.id)
    setIsArchiveDialogOpen(false)
    navigate({ to: '/projects' })
  }

  const handleRoleChange = async (memberUserId: Guid, role: ProjectRole) => {
    await updateMemberRoleMutation.mutateAsync({ projectId, userId: memberUserId, data: { role } })
  }

  const handleConfirmRemoveMember = async () => {
    if (!memberToRemove || removeMemberMutation.isPending) return
    await removeMemberMutation.mutateAsync({ projectId, userId: memberToRemove.userId })
    setMemberToRemove(null)
  }

  const handleConfirmTransferOwnership = async () => {
    if (!memberToTransferTo || transferOwnershipMutation.isPending) return
    await transferOwnershipMutation.mutateAsync({
      projectId,
      data: { newOwnerUserId: memberToTransferTo.userId },
    })
    setMemberToTransferTo(null)
  }

  const isSaving = updateProjectMutation.isPending

  return (
    <Box
      sx={{
        px: { xs: 2, sm: 3, md: 6 },
        pb: 4,
        pt: { xs: 2, sm: 3 },
        maxWidth: 900,
        mx: 'auto',
      }}
    >
      <Stack spacing={{ xs: 2, sm: 3 }}>
        <Box>
          <Typography
            variant="h5"
            sx={{ fontWeight: 700, mb: 0.5, fontSize: { xs: '1.25rem', sm: '1.5rem' } }}
          >
            Workspace Settings
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Configure your workspace workflow, details, and team access permissions.
          </Typography>
        </Box>

        {projectQuery.isLoading ? (
          <Typography color="text.secondary">Loading workspace...</Typography>
        ) : null}
        {projectQuery.isError ? (
          <Alert severity="error">Unable to load workspace settings.</Alert>
        ) : null}

        {project ? (
          <Card variant="outlined">
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
              <Stack spacing={3}>
                <Stack direction="row" spacing={1} alignItems="center">
                  <InfoOutlinedIcon sx={{ fontSize: 20, color: 'primary.main' }} />
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>
                    General Details
                  </Typography>
                </Stack>

                <Stack spacing={1}>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    Workspace Name
                  </Typography>
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={{ xs: 1.5, sm: 2 }}
                    alignItems={{ xs: 'stretch', sm: 'center' }}
                  >
                    <TextField
                      value={name}
                      onChange={(event) => setName(event.target.value)}
                      disabled={!isOwner || isSaving}
                      placeholder="Enter workspace name"
                      size="small"
                      fullWidth
                      sx={{ flex: 1 }}
                    />
                    {isOwner ? (
                      <Button
                        variant="contained"
                        onClick={handleSave}
                        disabled={!canSave}
                        sx={{ width: { xs: '100%', sm: 'auto' }, flexShrink: 0 }}
                      >
                        {isSaving ? 'Saving...' : 'Save Changes'}
                      </Button>
                    ) : null}
                  </Stack>
                </Stack>

                {updateProjectMutation.isError ? (
                  <Alert severity="error">Unable to update workspace name.</Alert>
                ) : null}
              </Stack>
            </CardContent>
          </Card>
        ) : null}

        {project ? (
          <Card variant="outlined">
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
              <Stack spacing={3}>
                <Box>
                  <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
                    <GroupsOutlinedIcon sx={{ fontSize: 20, color: 'primary.main' }} />
                    <Typography variant="h6" sx={{ fontWeight: 700 }}>
                      Team Management
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Manage who has access to this workspace and their roles.
                  </Typography>
                </Box>

                {canManageMembers ? (
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1.5}
                    alignItems={{ xs: 'stretch', sm: 'flex-start' }}
                  >
                    <TextField
                      value={inviteEmail}
                      onChange={(event) => setInviteEmail(event.target.value)}
                      placeholder="Enter colleague's email..."
                      size="small"
                      disabled={inviteUserMutation.isPending}
                      fullWidth
                      sx={{ flex: 1 }}
                      slotProps={{
                        input: {
                          startAdornment: (
                            <InputAdornment position="start">
                              <EmailOutlinedIcon sx={{ fontSize: 18, color: 'text.secondary' }} />
                            </InputAdornment>
                          ),
                        },
                      }}
                    />
                    <Stack
                      direction="row"
                      spacing={1.5}
                      sx={{ width: { xs: '100%', sm: 'auto' } }}
                    >
                      <Select
                        value={inviteRole}
                        onChange={(event) => setInviteRole(event.target.value)}
                        size="small"
                        sx={{ minWidth: 120, flex: { xs: 1, sm: 'none' } }}
                      >
                        {isOwner ? <MenuItem value="1">Manager</MenuItem> : null}
                        <MenuItem value="2">Member</MenuItem>
                        <MenuItem value="3">Viewer</MenuItem>
                      </Select>
                      <Button
                        variant="outlined"
                        onClick={handleInvite}
                        disabled={inviteUserMutation.isPending || !isValidEmail(inviteEmail)}
                        sx={{ flex: { xs: 1, sm: 'none' }, flexShrink: 0, whiteSpace: 'nowrap' }}
                      >
                        {inviteUserMutation.isPending ? 'Inviting...' : 'Invite User'}
                      </Button>
                    </Stack>
                  </Stack>
                ) : null}

                {inviteUserMutation.isError ? (
                  <Alert severity="error">Unable to send invitation.</Alert>
                ) : null}
                {updateMemberRoleMutation.isError ? (
                  <Alert severity="error">Unable to update member role.</Alert>
                ) : null}
                {removeMemberMutation.isError ? (
                  <Alert severity="error">Unable to remove member.</Alert>
                ) : null}
                {transferOwnershipMutation.isError ? (
                  <Alert severity="error">Unable to transfer ownership.</Alert>
                ) : null}

                <Stack spacing={0} divider={<Divider />}>
                  {members.map((member) => {
                    const isCurrentUser = member.userId === currentUserId
                    const isOwnerMember = project.ownerId === member.userId || member.role === 0
                    const canChangeRole = canManageMembers && !isCurrentUser && !isOwnerMember
                    const canRemove = canManageMembers && !isCurrentUser && !isOwnerMember
                    const canTransferOwnership = isOwner && !isCurrentUser && !isOwnerMember

                    const displayName =
                      member.userName ??
                      member.user?.userName ??
                      member.email ??
                      member.user?.email ??
                      'Unknown'
                    const displayEmail = member.email ?? member.user?.email ?? ''
                    const roleLabel = projectRoleLabel(member.role)

                    return (
                      <Stack
                        key={member.userId}
                        direction="row"
                        spacing={{ xs: 1.5, sm: 2 }}
                        alignItems="center"
                        useFlexGap
                        sx={{
                          py: 2,
                          flexWrap: { xs: 'wrap', sm: 'nowrap' },
                          rowGap: { xs: 1.5, sm: 0 },
                        }}
                      >
                        <UserAvatar
                          userId={member.userId}
                          name={displayName}
                          sx={{
                            width: 40,
                            height: 40,
                            bgcolor: 'action.hover',
                            color: 'text.primary',
                            fontSize: 14,
                            fontWeight: 600,
                            flexShrink: 0,
                          }}
                        />
                        <Box
                          sx={{
                            flex: 1,
                            minWidth: 0,
                            // On mobile, fill the remaining space next to the avatar
                            // so the actions wrap to a second row below.
                            width: { xs: 'calc(100% - 56px)', sm: 'auto' },
                          }}
                        >
                          <Typography
                            variant="body2"
                            noWrap
                            sx={{
                              fontWeight: 600,
                              textOverflow: 'ellipsis',
                              overflow: 'hidden',
                            }}
                          >
                            {displayName}
                            {isCurrentUser ? ' (You)' : ''}
                          </Typography>
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            noWrap
                            sx={{
                              display: 'block',
                              textOverflow: 'ellipsis',
                              overflow: 'hidden',
                            }}
                          >
                            {displayEmail}
                          </Typography>
                        </Box>

                        <Box
                          sx={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 1,
                            flexShrink: 0,
                            width: { xs: '100%', sm: 'auto' },
                            justifyContent: { xs: 'space-between', sm: 'flex-end' },
                          }}
                        >
                          {canChangeRole ? (
                            <Select
                              size="small"
                              value={String(member.role)}
                              onChange={(event) =>
                                handleRoleChange(
                                  member.userId,
                                  Number(event.target.value) as ProjectRole,
                                )
                              }
                              disabled={updateMemberRoleMutation.isPending}
                              sx={{ minWidth: { xs: 110, sm: 120 }, flex: { xs: 1, sm: 'none' } }}
                            >
                              {isOwner || member.role === 1 ? (
                                <MenuItem value="1">Manager</MenuItem>
                              ) : null}
                              <MenuItem value="2">Member</MenuItem>
                              <MenuItem value="3">Viewer</MenuItem>
                            </Select>
                          ) : (
                            <Chip
                              label={roleLabel.toUpperCase()}
                              size="small"
                              variant="outlined"
                              sx={{ fontWeight: 600, fontSize: 11 }}
                            />
                          )}

                          {canTransferOwnership ? (
                            <IconButton
                              size="small"
                              aria-label={`Transfer ownership to ${displayName}`}
                              title="Transfer ownership"
                              onClick={() => setMemberToTransferTo(member)}
                              disabled={transferOwnershipMutation.isPending}
                            >
                              <AdminPanelSettingsOutlinedIcon sx={{ fontSize: 18 }} />
                            </IconButton>
                          ) : null}

                          {canRemove ? (
                            <IconButton
                              size="small"
                              aria-label={`Remove ${displayName}`}
                              onClick={() => setMemberToRemove(member)}
                              disabled={removeMemberMutation.isPending}
                            >
                              <PersonRemoveOutlinedIcon sx={{ fontSize: 18 }} />
                            </IconButton>
                          ) : null}
                        </Box>
                      </Stack>
                    )
                  })}

                  {!membersQuery.isLoading && members.length === 0 ? (
                    <EmptyState
                      icon={PeopleOutlineIcon}
                      title="Just you for now"
                      description="Invite teammates to start collaborating on this workspace."
                      compact
                    />
                  ) : null}
                </Stack>
              </Stack>
            </CardContent>
          </Card>
        ) : null}

        {project && isOwner ? (
          <Card variant="outlined" sx={{ borderColor: 'warning.main', borderWidth: 1 }}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                justifyContent="space-between"
                alignItems={{ xs: 'stretch', sm: 'center' }}
              >
                <Box>
                  <Typography variant="h6" color="warning" sx={{ fontWeight: 700 }}>
                    Archive Workspace
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Remove this workspace and all its data.
                  </Typography>
                </Box>
                <Button
                  color="warning"
                  variant="outlined"
                  onClick={() => setIsArchiveDialogOpen(true)}
                  disabled={archiveProjectMutation.isPending}
                  sx={{ flexShrink: 0, width: { xs: '100%', sm: 'auto' } }}
                >
                  {archiveProjectMutation.isPending ? 'Archiving...' : 'Archive Workspace'}
                </Button>
              </Stack>
              {archiveProjectMutation.isError ? (
                <Alert severity="warning" sx={{ mt: 2 }}>
                  Unable to archive workspace.
                </Alert>
              ) : null}
            </CardContent>
          </Card>
        ) : null}
      </Stack>

      <Dialog
        open={isOwner && isArchiveDialogOpen}
        onClose={() => setIsArchiveDialogOpen(false)}
        maxWidth="xs"
        fullWidth
        aria-labelledby="archive-dialog-title"
      >
        <DialogTitle id="archive-dialog-title">Archive Workspace</DialogTitle>
        <DialogContent>
          <DialogContentText>Are you sure you want to archive this workspace?</DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setIsArchiveDialogOpen(false)}
            disabled={archiveProjectMutation.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleArchiveProject}
            color="warning"
            variant="contained"
            disabled={archiveProjectMutation.isPending}
          >
            Confirm archive
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(memberToRemove)}
        onClose={() => {
          if (!removeMemberMutation.isPending) setMemberToRemove(null)
        }}
        maxWidth="xs"
        fullWidth
        aria-labelledby="remove-member-dialog-title"
      >
        <DialogTitle id="remove-member-dialog-title">Remove Member</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to remove{' '}
            {memberToRemove?.userName ??
              memberToRemove?.user?.userName ??
              memberToRemove?.email ??
              'this member'}{' '}
            from this workspace?
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMemberToRemove(null)} disabled={removeMemberMutation.isPending}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirmRemoveMember}
            color="error"
            variant="contained"
            disabled={removeMemberMutation.isPending}
          >
            {removeMemberMutation.isPending ? 'Removing...' : 'Confirm Remove'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(memberToTransferTo)}
        onClose={() => {
          if (!transferOwnershipMutation.isPending) setMemberToTransferTo(null)
        }}
        maxWidth="xs"
        fullWidth
        aria-labelledby="transfer-dialog-title"
      >
        <DialogTitle id="transfer-dialog-title">Transfer Ownership</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Transfer workspace ownership to{' '}
            {memberToTransferTo?.userName ??
              memberToTransferTo?.user?.userName ??
              memberToTransferTo?.email ??
              'this member'}
            ? You will become a regular member after this action.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setMemberToTransferTo(null)}
            disabled={transferOwnershipMutation.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleConfirmTransferOwnership}
            color="warning"
            variant="contained"
            disabled={transferOwnershipMutation.isPending}
          >
            {transferOwnershipMutation.isPending ? 'Transferring...' : 'Confirm Transfer'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
