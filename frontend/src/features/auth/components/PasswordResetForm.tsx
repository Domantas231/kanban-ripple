import { useState } from 'react'
import { Link as RouterLink } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import LinearProgress from '@mui/material/LinearProgress'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import axios from 'axios'
import { requestPasswordReset, resetPassword } from '@/features/auth/api/auth'
import type { ErrorResponse } from '@/lib/types'
import { AuthLayout } from './AuthLayout'

const requestSchema = z.object({
  email: z.string().trim().email('Enter a valid email address'),
})

const resetSchema = z
  .object({
    newPassword: z
      .string()
      .min(8, 'Password must be at least 8 characters')
      .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
      .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
      .regex(/[0-9]/, 'Password must contain at least one digit')
      .regex(/[^a-zA-Z0-9]/, 'Password must contain at least one special character'),
    confirmPassword: z.string().min(1, 'Confirm your new password'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Passwords do not match',
  })

const passwordRequirements = [
  { label: 'At least 8 characters', test: (pw: string) => pw.length >= 8 },
  { label: 'One uppercase letter', test: (pw: string) => /[A-Z]/.test(pw) },
  { label: 'One lowercase letter', test: (pw: string) => /[a-z]/.test(pw) },
  { label: 'One digit', test: (pw: string) => /[0-9]/.test(pw) },
  { label: 'One special character', test: (pw: string) => /[^a-zA-Z0-9]/.test(pw) },
] as const

function getStrengthScore(password: string): number {
  if (!password) return 0
  return passwordRequirements.filter((req) => req.test(password)).length
}

function getStrengthColor(score: number): 'error' | 'warning' | 'primary' {
  if (score <= 2) return 'error'
  if (score <= 4) return 'warning'
  return 'primary'
}

type RequestFormValues = z.infer<typeof requestSchema>
type ResetFormValues = z.infer<typeof resetSchema>

type PasswordResetFormProps = {
  token?: string
  encodedEmail?: string
}

function tryDecodeBase64Url(value?: string): string | null {
  if (!value) {
    return null
  }

  try {
    const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
    const padding = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4))
    return window.atob(base64 + padding)
  } catch {
    return null
  }
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (!axios.isAxiosError(error)) {
    return fallback
  }

  const payload = error.response?.data as ErrorResponse | { message?: string } | undefined
  if (!payload) {
    return fallback
  }

  if ('error' in payload) {
    return payload.error.message || fallback
  }

  return payload.message || fallback
}

export function PasswordResetForm({ token, encodedEmail }: PasswordResetFormProps) {
  const [requestCompleted, setRequestCompleted] = useState(false)
  const [resetCompleted, setResetCompleted] = useState(false)
  const [isExpiredToken, setIsExpiredToken] = useState(false)
  const decodedEmail = tryDecodeBase64Url(encodedEmail)
  const isResetMode = Boolean(token)

  const {
    register: registerRequestField,
    handleSubmit: handleRequestSubmit,
    formState: { errors: requestErrors, isSubmitting: isRequestSubmitting },
    setError: setRequestError,
  } = useForm<RequestFormValues>({
    resolver: zodResolver(requestSchema),
    defaultValues: {
      email: '',
    },
  })

  const {
    register: registerResetField,
    handleSubmit: handleResetSubmit,
    formState: { errors: resetErrors, isSubmitting: isResetSubmitting },
    setError: setResetError,
    control: resetControl,
  } = useForm<ResetFormValues>({
    resolver: zodResolver(resetSchema),
    defaultValues: {
      newPassword: '',
      confirmPassword: '',
    },
  })

  const newPasswordValue = useWatch({ control: resetControl, name: 'newPassword' })
  const strengthScore = getStrengthScore(newPasswordValue || '')
  const strengthColor = getStrengthColor(strengthScore)

  const onRequestSubmit = handleRequestSubmit(async (values) => {
    try {
      await requestPasswordReset({ email: values.email })
      setRequestCompleted(true)
    } catch {
      setRequestCompleted(false)
      setRequestError('root.server', {
        type: 'server',
        message: 'Unable to process your request right now. Please try again.',
      })
    }
  })

  const onResetSubmit = handleResetSubmit(async (values) => {
    if (!token || !decodedEmail) {
      setResetError('root.server', {
        type: 'server',
        message: 'This password reset link is invalid. Request a new link.',
      })
      setIsExpiredToken(true)
      return
    }

    try {
      await resetPassword({
        email: decodedEmail,
        token,
        newPassword: values.newPassword,
      })

      setResetCompleted(true)
    } catch (error) {
      const message = getErrorMessage(error, 'Unable to reset password. Please request a new link.')

      if (/expired|invalid or expired|reset token/i.test(message)) {
        setIsExpiredToken(true)
        setResetError('root.server', {
          type: 'server',
          message: 'This reset link has expired.',
        })
        return
      }

      setResetError('root.server', {
        type: 'server',
        message,
      })
    }
  })

  if (resetCompleted) {
    return (
      <AuthLayout>
        <Stack spacing={3} sx={{ alignItems: 'center', py: 2 }}>
          <Box
            sx={{
              '@keyframes scaleIn': {
                from: { transform: 'scale(0)', opacity: 0 },
                to: { transform: 'scale(1)', opacity: 1 },
              },
              animation: 'scaleIn 300ms ease-out',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 64,
              height: 64,
              borderRadius: '50%',
              bgcolor: 'primary.main',
            }}
          >
            <CheckCircleOutlineIcon sx={{ fontSize: 36, color: '#FFFFFF' }} />
          </Box>
          <Stack spacing={0.5} sx={{ textAlign: 'center' }}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              Password reset successful
            </Typography>
          </Stack>
          <Button
            component={RouterLink}
            to="/login"
            variant="outlined"
            sx={{ mt: 1 }}
          >
            Sign in now
          </Button>
        </Stack>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      {!isResetMode ? (
        <Stack component="form" onSubmit={onRequestSubmit} spacing={3} noValidate>
          <Stack spacing={0.5}>
            <Typography variant="h4" sx={{ fontWeight: 700 }}>
              Reset password
            </Typography>
            <Typography variant="body1" color="text.secondary">
              Enter your email and we&apos;ll send you a reset link.
            </Typography>
          </Stack>

          {requestCompleted ? (
            <Alert severity="success" sx={{ borderRadius: 1.5 }}>
              If that email exists then a reset password email was sent.
            </Alert>
          ) : null}

          {requestErrors.root?.server?.message ? (
            <Alert severity="error" sx={{ borderRadius: 1.5 }}>
              {requestErrors.root.server.message}
            </Alert>
          ) : null}

          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(requestErrors.email)}
            helperText={requestErrors.email?.message}
            {...registerRequestField('email')}
          />

          <Button
            type="submit"
            variant="contained"
            disabled={isRequestSubmitting}
            sx={{
              height: 48,
              fontSize: '0.9375rem',
              fontWeight: 600,
            }}
          >
            {isRequestSubmitting ? 'Sending...' : 'Send reset link'}
          </Button>

          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
            Back to{' '}
            <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 600 }}>
              sign in
            </Link>
          </Typography>
        </Stack>
      ) : (
        <Stack component="form" onSubmit={onResetSubmit} spacing={3} noValidate>
          <Stack spacing={0.5}>
            <Typography variant="h4" sx={{ fontWeight: 700 }}>
              Choose a new password
            </Typography>
            <Typography variant="body1" color="text.secondary">
              Enter and confirm your new password.
            </Typography>
          </Stack>

          {resetErrors.root?.server?.message ? (
            <Alert severity="error" sx={{ borderRadius: 1.5 }}>
              {resetErrors.root.server.message}
            </Alert>
          ) : null}

          {isExpiredToken ? (
            <Button
              component={RouterLink}
              to="/reset-password"
              variant="outlined"
              sx={{
                height: 48,
                fontSize: '0.9375rem',
                fontWeight: 600,
              }}
            >
              Request a new link
            </Button>
          ) : (
            <>
              <Box>
                <TextField
                  label="New password"
                  type="password"
                  autoComplete="new-password"
                  fullWidth
                  error={Boolean(resetErrors.newPassword)}
                  helperText={resetErrors.newPassword?.message}
                  {...registerResetField('newPassword')}
                />

                {newPasswordValue ? (
                  <Box sx={{ mt: 1.5 }}>
                    <LinearProgress
                      variant="determinate"
                      value={(strengthScore / 5) * 100}
                      color={strengthColor}
                      sx={{
                        height: 4,
                        borderRadius: 2,
                        bgcolor: 'action.hover',
                      }}
                    />

                    <Stack spacing={0.5} sx={{ mt: 1.5 }}>
                      {passwordRequirements.map((req) => {
                        const met = req.test(newPasswordValue)
                        return (
                          <Box
                            key={req.label}
                            sx={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: 0.75,
                            }}
                          >
                            {met ? (
                              <CheckCircleOutlineIcon
                                sx={{ fontSize: 16, color: 'primary.main' }}
                              />
                            ) : (
                              <RadioButtonUncheckedIcon
                                sx={{ fontSize: 16, color: 'text.disabled' }}
                              />
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
                label="Confirm new password"
                type="password"
                autoComplete="new-password"
                fullWidth
                error={Boolean(resetErrors.confirmPassword)}
                helperText={resetErrors.confirmPassword?.message}
                {...registerResetField('confirmPassword')}
              />

              <Button
                type="submit"
                variant="contained"
                disabled={isResetSubmitting}
                sx={{
                  height: 48,
                  fontSize: '0.9375rem',
                  fontWeight: 600,
                }}
              >
                {isResetSubmitting ? 'Resetting...' : 'Reset password'}
              </Button>
            </>
          )}

          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
            Back to{' '}
            <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 600 }}>
              sign in
            </Link>
          </Typography>
        </Stack>
      )}
    </AuthLayout>
  )
}
