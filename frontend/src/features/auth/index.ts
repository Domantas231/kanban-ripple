export {
  login,
  logout,
  refresh,
  register,
  confirmEmail,
  resendConfirmation,
  requestPasswordReset,
  resetPassword,
  changePassword,
  updateDisplayName,
  deleteAccount,
  uploadProfilePhoto,
  deleteProfilePhoto,
  getUserProfilePhoto,
} from './api/auth'
export { authQueryKeys } from './api/query-keys'
export { useAuthStore } from './stores/authStore'
export { LoginForm } from './components/LoginForm'
export { RegistrationForm } from './components/RegistrationForm'
export { ConfirmEmailForm } from './components/ConfirmEmailForm'
export { PasswordResetForm } from './components/PasswordResetForm'
export { UserAvatar } from './components/UserAvatar'
export {
  isAuthenticated,
  redirectIfAuthenticated,
  requireAuthenticated,
} from './utils/guards'
export {
  passwordRequirements,
  getPasswordStrengthScore,
  getPasswordStrengthColor,
  extractErrorMessage,
} from './utils/passwordValidation'
