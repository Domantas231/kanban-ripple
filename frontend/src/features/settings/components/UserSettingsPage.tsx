import { useEffect, useRef, useState } from 'react'
import { useNavigate, useRouterState } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import LinearProgress from '@mui/material/LinearProgress'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Typography from '@mui/material/Typography'
import BrushOutlinedIcon from '@mui/icons-material/BrushOutlined'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import ComputerOutlinedIcon from '@mui/icons-material/ComputerOutlined'
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline'
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined'
import LockOutlinedIcon from '@mui/icons-material/LockOutlined'
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked'
import PersonOutlineIcon from '@mui/icons-material/PersonOutline'
import { GoogleAccountSection } from '@/features/settings/components/GoogleAccountSection'
import { SubscriptionsSection } from '@/features/subscriptions'
import {
  changePassword,
  deleteAccount,
  deleteProfilePhoto,
  getUserProfilePhoto,
  updateDisplayName,
  uploadProfilePhoto,
} from '@/features/auth'
import { useAuthStore } from '@/features/auth'
import {
  extractErrorMessage,
  getPasswordStrengthColor,
  getPasswordStrengthScore,
  passwordRequirements,
} from '@/features/auth'
import { authQueryKeys } from '@/features/auth'
import { useUiStore, type ThemeMode } from '@/stores/uiStore'

export function UserSettingsPage() {
  const search = useRouterState({
    select: (state) => state.location.search as Record<string, unknown>,
  })
  const navigate = useNavigate()
  const toastShown = useRef(false)
  const enqueueToast = useUiStore((state) => state.enqueueToast)

  const queryClient = useQueryClient()
  const user = useAuthStore((s) => s.user)
  const setAuth = useAuthStore((s) => s.setAuth)
  const clearAuth = useAuthStore((s) => s.clearAuth)
  const accessToken = useAuthStore((s) => s.accessToken)
  const initials = (user?.userName ?? user?.email)?.charAt(0).toUpperCase() ?? '?'

  const fileInputRef = useRef<HTMLInputElement>(null)

  const profilePhotoQuery = useQuery({
    queryKey: authQueryKeys.userProfilePhoto(user?.id ?? ''),
    queryFn: () => getUserProfilePhoto(user!.id),
    enabled: Boolean(user?.id),
    staleTime: 5 * 60 * 1000,
  })
  const photoUrl = profilePhotoQuery.data ?? null

  const themeMode = useUiStore((s) => s.themeMode)
  const setThemeMode = useUiStore((s) => s.setThemeMode)

  const [displayName, setDisplayName] = useState(user?.userName ?? user?.email?.split('@')[0] ?? '')
  const [displayNameSaving, setDisplayNameSaving] = useState(false)
  const displayNameChanged = displayName !== (user?.userName ?? user?.email?.split('@')[0] ?? '')

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordSaving, setPasswordSaving] = useState(false)

  const [photoUploading, setPhotoUploading] = useState(false)

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)

  async function handlePhotoUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''

    if (file.size > 2 * 1024 * 1024) {
      enqueueToast({ message: 'Photo must be under 2 MB.', severity: 'error' })
      return
    }

    setPhotoUploading(true)
    try {
      await uploadProfilePhoto(file)
      await queryClient.invalidateQueries({ queryKey: authQueryKeys.userProfilePhoto(user!.id) })
      enqueueToast({ message: 'Profile photo updated.', severity: 'success' })
    } catch (err) {
      enqueueToast({ message: extractErrorMessage(err, 'Failed to upload photo.'), severity: 'error' })
    } finally {
      setPhotoUploading(false)
    }
  }

  async function handlePhotoRemove() {
    setPhotoUploading(true)
    try {
      await deleteProfilePhoto()
      await queryClient.invalidateQueries({ queryKey: authQueryKeys.userProfilePhoto(user!.id) })
      enqueueToast({ message: 'Profile photo removed.', severity: 'success' })
    } catch (err) {
      enqueueToast({ message: extractErrorMessage(err, 'Failed to remove photo.'), severity: 'error' })
    } finally {
      setPhotoUploading(false)
    }
  }

  async function handleSaveDisplayName() {
    if (!displayName.trim()) return
    setDisplayNameSaving(true)
    try {
      const result = await updateDisplayName({ displayName: displayName.trim() })
      if (user && accessToken) {
        setAuth({ ...user, userName: result.displayName }, accessToken)
      }
      enqueueToast({ message: 'Display name updated.', severity: 'success' })
    } catch (err) {
      enqueueToast({ message: extractErrorMessage(err, 'Failed to update display name.'), severity: 'error' })
    } finally {
      setDisplayNameSaving(false)
    }
  }

  async function handleChangePassword() {
    if (newPassword !== confirmPassword) {
      enqueueToast({ message: 'New passwords do not match.', severity: 'error' })
      return
    }
    const failedRequirement = passwordRequirements.find((req) => !req.test(newPassword))
    if (failedRequirement) {
      enqueueToast({ message: failedRequirement.label + ' required.', severity: 'error' })
      return
    }
    if (newPassword === currentPassword) {
      enqueueToast({ message: 'New password must be different from current password.', severity: 'error' })
      return
    }
    setPasswordSaving(true)
    try {
      await changePassword({ currentPassword, newPassword })
      enqueueToast({ message: 'Password changed successfully.', severity: 'success' })
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      enqueueToast({ message: extractErrorMessage(err, 'Failed to change password.'), severity: 'error' })
    } finally {
      setPasswordSaving(false)
    }
  }

  async function handleDeleteAccount() {
    setDeleting(true)
    try {
      await deleteAccount()
      clearAuth()
      navigate({ to: '/login' })
    } catch (err) {
      enqueueToast({ message: extractErrorMessage(err, 'Failed to delete account.'), severity: 'error' })
      setDeleting(false)
      setDeleteDialogOpen(false)
    }
  }

  useEffect(() => {
    if (toastShown.current) return
    const google = search.google as string | undefined
    if (google === 'connected') {
      toastShown.current = true
      enqueueToast({ message: 'Google account connected successfully.', severity: 'success' })
    } else if (google === 'error') {
      toastShown.current = true
      enqueueToast({ message: 'Failed to connect Google account. Please try again.', severity: 'error' })
    }
  }, [search.google, enqueueToast])

  const passwordStrengthScore = getPasswordStrengthScore(newPassword)
  const passwordStrengthColor = getPasswordStrengthColor(passwordStrengthScore)
  const allPasswordRequirementsMet = passwordStrengthScore === passwordRequirements.length
  const passwordFormValid =
    currentPassword.length > 0 &&
    allPasswordRequirementsMet &&
    newPassword === confirmPassword &&
    newPassword !== currentPassword

  return (
    <Box sx={{ px: { xs: 0, sm: 3, md: 6 }, pb: 4, pt: { xs: 1, sm: 3 }, maxWidth: 900, mx: 'auto' }}>
      <Stack spacing={{ xs: 2, sm: 3 }}>
        <Box>
          <Typography
            variant="h5"
            sx={{
              fontWeight: 700,
              mb: 0.5,
              fontSize: { xs: '1.25rem', sm: '1.5rem' },
              lineHeight: 1.2,
            }}
            component="h1"
          >
            Account Settings
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Manage your profile, preferences, and integrations.
          </Typography>
        </Box>

        <Card variant="outlined">
          <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
            <Stack spacing={{ xs: 2.5, sm: 3 }}>
              <Stack direction="row" spacing={1} alignItems="center">
                <PersonOutlineIcon sx={{ fontSize: 20, color: 'primary.main' }} />
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Profile
                </Typography>
              </Stack>

              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={{ xs: 1.5, sm: 2.5 }}
                alignItems={{ xs: 'flex-start', sm: 'center' }}
              >
                <Avatar
                  src={photoUrl ?? undefined}
                  sx={{
                    width: { xs: 64, sm: 72 },
                    height: { xs: 64, sm: 72 },
                    fontSize: '1.75rem',
                    fontWeight: 600,
                    bgcolor: 'primary.main',
                    color: 'primary.contrastText',
                    border: '1px solid',
                    borderColor: 'common.white',
                    boxSizing: 'border-box',
                    flexShrink: 0,
                  }}
                >
                  {initials}
                </Avatar>
                <Box sx={{ minWidth: 0, width: '100%' }}>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    Profile Photo
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                    JPG, PNG, GIF or WebP. Max 2 MB.
                  </Typography>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/jpeg,image/png,image/gif,image/webp"
                    hidden
                    onChange={handlePhotoUpload}
                  />
                  <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                    <Button
                      variant="outlined"
                      size="small"
                      disabled={photoUploading}
                      onClick={() => fileInputRef.current?.click()}
                    >
                      {photoUploading ? 'Uploading...' : 'Upload Photo'}
                    </Button>
                    {photoUrl && (
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        disabled={photoUploading}
                        onClick={handlePhotoRemove}
                      >
                        Remove
                      </Button>
                    )}
                  </Stack>
                </Box>
              </Stack>

              <Divider />

              <Stack spacing={1}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  Display Name
                </Typography>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={{ xs: 1, sm: 1.5 }}
                  alignItems={{ xs: 'stretch', sm: 'flex-start' }}
                  sx={{ maxWidth: { xs: '100%', sm: 400 } }}
                >
                  <TextField
                    size="small"
                    fullWidth
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    placeholder="Enter your display name"
                    slotProps={{ htmlInput: { maxLength: 50 } }}
                  />
                  <Button
                    variant="contained"
                    size="small"
                    disabled={!displayNameChanged || !displayName.trim() || displayNameSaving}
                    onClick={handleSaveDisplayName}
                    sx={{
                      whiteSpace: 'nowrap',
                      minWidth: { sm: 'auto' },
                      py: 0.9,
                      alignSelf: { xs: 'flex-start', sm: 'auto' },
                    }}
                  >
                    {displayNameSaving ? 'Saving...' : 'Save'}
                  </Button>
                </Stack>
              </Stack>

              <Stack spacing={1}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  Email Address
                </Typography>
                <TextField
                  size="small"
                  fullWidth
                  value={user?.email ?? ''}
                  disabled
                  sx={{ maxWidth: { xs: '100%', sm: 400 } }}
                />
              </Stack>
            </Stack>
          </CardContent>
        </Card>

        <Card variant="outlined">
          <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
            <Stack spacing={{ xs: 2.5, sm: 3 }}>
              <Stack direction="row" spacing={1} alignItems="center">
                <LockOutlinedIcon sx={{ fontSize: 20, color: 'primary.main' }} />
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Change Password
                </Typography>
              </Stack>

              <Stack spacing={2} sx={{ maxWidth: { xs: '100%', sm: 400 } }}>
                <TextField
                  size="small"
                  fullWidth
                  type="password"
                  label="Current Password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  autoComplete="current-password"
                />
                <Box>
                  <TextField
                    size="small"
                    fullWidth
                    type="password"
                    label="New Password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    autoComplete="new-password"
                    error={newPassword.length > 0 && newPassword === currentPassword}
                    helperText={
                      newPassword.length > 0 && newPassword === currentPassword
                        ? 'New password must be different from current password.'
                        : undefined
                    }
                  />
                  {newPassword ? (
                    <Box sx={{ mt: 1.5 }}>
                      <LinearProgress
                        variant="determinate"
                        value={(passwordStrengthScore / passwordRequirements.length) * 100}
                        color={passwordStrengthColor}
                        sx={{ height: 4, borderRadius: 2, bgcolor: 'action.hover' }}
                      />
                      <Stack spacing={0.5} sx={{ mt: 1.5 }}>
                        {passwordRequirements.map((req) => {
                          const met = req.test(newPassword)
                          return (
                            <Box
                              key={req.label}
                              sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}
                            >
                              {met ? (
                                <CheckCircleOutlineIcon sx={{ fontSize: 16, color: 'primary.main' }} />
                              ) : (
                                <RadioButtonUncheckedIcon sx={{ fontSize: 16, color: 'text.disabled' }} />
                              )}
                              <Typography
                                variant="caption"
                                sx={{
                                  color: met ? 'text.primary' : 'text.disabled',
                                  fontSize: '0.75rem',
                                }}
                              >
                                {req.label}
                              </Typography>
                            </Box>
                          )
                        })}
                      </Stack>
                    </Box>
                  ) : null}
                </Box>
                <TextField
                  size="small"
                  fullWidth
                  type="password"
                  label="Confirm New Password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  autoComplete="new-password"
                  error={confirmPassword.length > 0 && newPassword !== confirmPassword}
                  helperText={
                    confirmPassword.length > 0 && newPassword !== confirmPassword
                      ? 'Passwords do not match.'
                      : undefined
                  }
                />
                <Box>
                  <Button
                    variant="contained"
                    size="small"
                    disabled={!passwordFormValid || passwordSaving}
                    onClick={handleChangePassword}
                    sx={{ width: { xs: '100%', sm: 'auto' } }}
                  >
                    {passwordSaving ? 'Changing...' : 'Change Password'}
                  </Button>
                </Box>
              </Stack>
            </Stack>
          </CardContent>
        </Card>

        <Card variant="outlined">
          <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
            <Stack spacing={{ xs: 2.5, sm: 3 }}>
              <Stack direction="row" spacing={1} alignItems="center">
                <BrushOutlinedIcon sx={{ fontSize: 20, color: 'primary.main' }} />
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Appearance
                </Typography>
              </Stack>

              <Stack spacing={1}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  Theme
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Choose how the app looks. System uses your OS preference.
                </Typography>
                <ToggleButtonGroup
                  value={themeMode}
                  exclusive
                  onChange={(_, value: ThemeMode | null) => {
                    if (value) setThemeMode(value)
                  }}
                  size="small"
                  sx={{
                    mt: 0.5,
                    width: { xs: '100%', sm: 'auto' },
                    alignSelf: { xs: 'stretch', sm: 'flex-start' },
                    '& .MuiToggleButton-root': {
                      px: { xs: 1, sm: 2 },
                      py: 0.75,
                      textTransform: 'none',
                      fontWeight: 500,
                      gap: 0.75,
                      flex: { xs: 1, sm: 'initial' },
                    },
                  }}
                >
                  <ToggleButton value="system" aria-label="System theme">
                    <ComputerOutlinedIcon sx={{ fontSize: 18 }} />
                    System
                  </ToggleButton>
                  <ToggleButton value="light" aria-label="Light theme">
                    <LightModeOutlinedIcon sx={{ fontSize: 18 }} />
                    Light
                  </ToggleButton>
                  <ToggleButton value="dark" aria-label="Dark theme">
                    <DarkModeOutlinedIcon sx={{ fontSize: 18 }} />
                    Dark
                  </ToggleButton>
                </ToggleButtonGroup>
              </Stack>
            </Stack>
          </CardContent>
        </Card>

        <SubscriptionsSection />

        <GoogleAccountSection />

        <Card variant="outlined" sx={{ borderColor: 'error.main' }}>
          <CardContent sx={{ p: { xs: 1.5, sm: 3 } }}>
            <Stack spacing={2}>
              <Stack direction="row" spacing={1} alignItems="center">
                <DeleteOutlineIcon sx={{ fontSize: 20, color: 'error.main' }} />
                <Typography variant="h6" sx={{ fontWeight: 700, color: 'error.main' }}>
                  Danger Zone
                </Typography>
              </Stack>

              <Typography variant="body2" color="text.secondary">
                Permanently delete your account and all associated data. This action cannot be undone. You must
                transfer ownership of any projects you own before deleting your account.
              </Typography>

              <Box>
                <Button
                  variant="outlined"
                  color="error"
                  size="small"
                  onClick={() => setDeleteDialogOpen(true)}
                  sx={{ width: { xs: '100%', sm: 'auto' } }}
                >
                  Delete Account
                </Button>
              </Box>
            </Stack>
          </CardContent>
        </Card>
      </Stack>

      <Dialog
        open={deleteDialogOpen}
        onClose={() => {
          if (!deleting) {
            setDeleteDialogOpen(false)
          }
        }}
        aria-labelledby="delete-account-dialog-title"
        aria-describedby="delete-account-dialog-description"
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle id="delete-account-dialog-title">Delete Account</DialogTitle>
        <DialogContent>
          <DialogContentText id="delete-account-dialog-description">
            This will permanently delete your account and all associated data. This action cannot be undone.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteDialogOpen(false)} disabled={deleting}>
            Cancel
          </Button>
          <Button color="error" variant="contained" disabled={deleting} onClick={handleDeleteAccount}>
            {deleting ? 'Deleting...' : 'Delete Account'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
