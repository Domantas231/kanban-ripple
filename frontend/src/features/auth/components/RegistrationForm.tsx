import { useState } from 'react'
import { Link as RouterLink } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import LinearProgress from '@mui/material/LinearProgress'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import axios, { type AxiosError } from 'axios'
import { register } from '@/features/auth/api/auth'
import type { ErrorResponse, ValidationErrorItem } from '@/lib/types'
import { AuthLayout } from './AuthLayout'

const registrationSchema = z
  .object({
    email: z.string().trim().email('Enter a valid email address'),
    password: z
      .string()
      .min(8, 'Password must be at least 8 characters')
      .regex(/[A-Z]/, 'Password must contain at least one uppercase letter')
      .regex(/[a-z]/, 'Password must contain at least one lowercase letter')
      .regex(/[0-9]/, 'Password must contain at least one digit')
      .regex(/[^a-zA-Z0-9]/, 'Password must contain at least one special character'),
    confirmPassword: z.string().min(1, 'Confirm your password'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })

type RegistrationFormValues = z.infer<typeof registrationSchema>

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

function mapValidationErrors(
  validationErrors: ValidationErrorItem[] | null | undefined,
): Partial<Record<'email' | 'password', string>> {
  if (!validationErrors?.length) {
    return {}
  }

  const mapped: Partial<Record<'email' | 'password', string>> = {}

  for (const validationError of validationErrors) {
    const propertyName = validationError.propertyName.toLowerCase()

    if (propertyName.includes('email') && !mapped.email) {
      mapped.email = validationError.errorMessage
      continue
    }

    if (propertyName.includes('password') && !mapped.password) {
      mapped.password = validationError.errorMessage
    }
  }

  return mapped
}

function getApiErrorPayload(error: unknown): ErrorResponse | { message?: string } | undefined {
  if (!axios.isAxiosError(error)) {
    return undefined
  }

  return error.response?.data as ErrorResponse | { message?: string } | undefined
}

function getApiErrorInfo(error: unknown): {
  message: string
  validationErrors: ValidationErrorItem[] | null | undefined
} {
  const fallbackMessage = 'Unable to register with the provided information.'
  const payload = getApiErrorPayload(error)

  if (!payload) {
    return {
      message: fallbackMessage,
      validationErrors: undefined,
    }
  }

  if ('error' in payload) {
    return {
      message: payload.error.message || fallbackMessage,
      validationErrors: payload.error.validationErrors,
    }
  }

  return {
    message: payload.message || fallbackMessage,
    validationErrors: undefined,
  }
}

export function RegistrationForm() {
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null)

  const {
    register: registerField,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
    control,
  } = useForm<RegistrationFormValues>({
    resolver: zodResolver(registrationSchema),
    defaultValues: {
      email: '',
      password: '',
      confirmPassword: '',
    },
  })

  const passwordValue = useWatch({ control, name: 'password' })
  const strengthScore = getStrengthScore(passwordValue || '')
  const strengthColor = getStrengthColor(strengthScore)

  const onSubmit = handleSubmit(async (values) => {
    setSuccessMessage(null)

    try {
      const result = await register({
        email: values.email,
        password: values.password,
      })

      setSubmittedEmail(result.email ?? values.email)
      setSuccessMessage(
        result.message ??
          'Account created. Check your email for a confirmation link to activate your account.',
      )
    } catch (error) {
      const apiErrorInfo = getApiErrorInfo(error)
      const apiValidationErrors = mapValidationErrors(apiErrorInfo.validationErrors)

      if (apiValidationErrors.email) {
        setError('email', { type: 'server', message: apiValidationErrors.email })
      }

      if (apiValidationErrors.password) {
        setError('password', { type: 'server', message: apiValidationErrors.password })
      }

      const axiosError = error as AxiosError
      const apiMessage = apiErrorInfo.message
      const isDuplicateEmail =
        axiosError.response?.status === 409 ||
        /duplicate|already|exists|taken/i.test(apiMessage)

      if (isDuplicateEmail) {
        setError('email', {
          type: 'server',
          message: apiMessage || 'This email is already in use.',
        })
        return
      }

      const isPasswordError = /password/i.test(apiMessage)
      if (isPasswordError && !apiValidationErrors.password) {
        setError('password', {
          type: 'server',
          message: apiMessage,
        })
        return
      }

      if (!apiValidationErrors.email && !apiValidationErrors.password) {
        setError('root.server', {
          type: 'server',
          message: apiMessage,
        })
      }
    }
  })

  if (submittedEmail) {
    return (
      <AuthLayout>
        <Stack spacing={3}>
          <Stack spacing={0.5}>
            <Typography variant="h4" sx={{ fontWeight: 700 }}>
              Check your email
            </Typography>
            <Typography variant="body1" color="text.secondary">
              We sent a confirmation link to <strong>{submittedEmail}</strong>. Click the link in
              the email to activate your account.
            </Typography>
          </Stack>

          {successMessage ? (
            <Alert severity="success" sx={{ borderRadius: 1.5 }}>
              {successMessage}
            </Alert>
          ) : null}

          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
            Already confirmed?{' '}
            <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 600 }}>
              Sign in
            </Link>
          </Typography>
        </Stack>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <Stack component="form" onSubmit={onSubmit} spacing={3} noValidate>
        <Stack spacing={0.5}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Create an account
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Sign up to start organizing your projects.
          </Typography>
        </Stack>

        {successMessage ? (
          <Alert severity="success" sx={{ borderRadius: 1.5 }}>
            {successMessage}
          </Alert>
        ) : null}

        {errors.root?.server?.message ? (
          <Alert severity="error" sx={{ borderRadius: 1.5 }}>
            {errors.root.server.message}
          </Alert>
        ) : null}

        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          fullWidth
          error={Boolean(errors.email)}
          helperText={errors.email?.message}
          {...registerField('email')}
        />

        <Box>
          <TextField
            label="Password"
            type="password"
            autoComplete="new-password"
            fullWidth
            error={Boolean(errors.password)}
            helperText={errors.password?.message}
            {...registerField('password')}
          />

          {/* Password strength meter */}
          {passwordValue ? (
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

              {/* Requirements checklist */}
              <Stack spacing={0.5} sx={{ mt: 1.5 }}>
                {passwordRequirements.map((req) => {
                  const met = req.test(passwordValue)
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
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          fullWidth
          error={Boolean(errors.confirmPassword)}
          helperText={errors.confirmPassword?.message}
          {...registerField('confirmPassword')}
        />

        <Button
          type="submit"
          variant="contained"
          disabled={isSubmitting}
          sx={{
            height: 48,
            fontSize: '0.9375rem',
            fontWeight: 600,
          }}
        >
          {isSubmitting ? 'Creating account...' : 'Create account'}
        </Button>

        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
          Already have an account?{' '}
          <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 600 }}>
            Sign in
          </Link>
        </Typography>
      </Stack>
    </AuthLayout>
  )
}
