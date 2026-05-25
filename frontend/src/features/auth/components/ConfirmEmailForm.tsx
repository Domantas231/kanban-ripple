import { useEffect, useRef, useState } from 'react'
import { Link as RouterLink } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import axios from 'axios'
import { confirmEmail, resendConfirmation } from '@/features/auth/api/auth'
import type { ErrorResponse } from '@/lib/types'
import { AuthLayout } from './AuthLayout'

type ConfirmEmailFormProps = {
  token?: string
  encodedEmail?: string
}

type ConfirmationStatus = 'pending' | 'success' | 'error' | 'missing'

const resendSchema = z.object({
  email: z.string().trim().email('Enter a valid email address'),
})

type ResendFormValues = z.infer<typeof resendSchema>

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

export function ConfirmEmailForm({ token, encodedEmail }: ConfirmEmailFormProps) {
  const decodedEmail = tryDecodeBase64Url(encodedEmail)
  const hasInitiated = useRef(false)
  const [status, setStatus] = useState<ConfirmationStatus>(() =>
    token && decodedEmail ? 'pending' : 'missing',
  )
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [resendSuccessMessage, setResendSuccessMessage] = useState<string | null>(null)

  const {
    register: registerField,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<ResendFormValues>({
    resolver: zodResolver(resendSchema),
    defaultValues: {
      email: decodedEmail ?? '',
    },
  })

  useEffect(() => {
    if (hasInitiated.current) return
    if (!token || !decodedEmail) return

    hasInitiated.current = true

    confirmEmail({ email: decodedEmail, token })
      .then((result) => {
        setStatus('success')
        setSuccessMessage(result.message ?? 'Your email has been confirmed.')
      })
      .catch((error) => {
        setStatus('error')
        setErrorMessage(
          getErrorMessage(
            error,
            'We could not confirm your email. The link may have expired.',
          ),
        )
      })
  }, [token, decodedEmail])

  const onResendSubmit = handleSubmit(async (values) => {
    setResendSuccessMessage(null)

    try {
      const result = await resendConfirmation({ email: values.email })
      setResendSuccessMessage(
        result.message ??
          'If that email is registered, a new confirmation link has been sent.',
      )
    } catch (error) {
      setError('root.server', {
        type: 'server',
        message: getErrorMessage(
          error,
          'Unable to resend the confirmation email. Please try again.',
        ),
      })
    }
  })

  if (status === 'pending') {
    return (
      <AuthLayout>
        <Stack spacing={3} sx={{ alignItems: 'center', py: 2 }}>
          <CircularProgress />
          <Stack spacing={0.5} sx={{ textAlign: 'center' }}>
            <Typography variant="h5" sx={{ fontWeight: 700 }}>
              Confirming your email
            </Typography>
            <Typography variant="body1" color="text.secondary">
              Hang tight — this only takes a moment.
            </Typography>
          </Stack>
        </Stack>
      </AuthLayout>
    )
  }

  if (status === 'success') {
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
              Email confirmed
            </Typography>
            <Typography variant="body1" color="text.secondary">
              {successMessage ?? 'Your account is now active.'}
            </Typography>
          </Stack>
          <Button
            component={RouterLink}
            to="/login"
            variant="contained"
            sx={{
              height: 48,
              fontSize: '0.9375rem',
              fontWeight: 600,
              px: 4,
            }}
          >
            Sign in
          </Button>
        </Stack>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <Stack component="form" onSubmit={onResendSubmit} spacing={3} noValidate>
        <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 56,
              height: 56,
              borderRadius: '50%',
              bgcolor: 'action.hover',
            }}
          >
            <ErrorOutlineIcon sx={{ fontSize: 32, color: 'error.main' }} />
          </Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            {status === 'missing' ? 'Confirmation link missing' : 'Confirmation failed'}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {status === 'missing'
              ? 'This page requires a confirmation link from your email.'
              : errorMessage ?? 'We could not confirm your email.'}
          </Typography>
        </Stack>

        {resendSuccessMessage ? (
          <Alert severity="success" sx={{ borderRadius: 1.5 }}>
            {resendSuccessMessage}
          </Alert>
        ) : null}

        {errors.root?.server?.message ? (
          <Alert severity="error" sx={{ borderRadius: 1.5 }}>
            {errors.root.server.message}
          </Alert>
        ) : null}

        <Stack spacing={1.5}>
          <Typography variant="body2" color="text.secondary">
            Enter your email and we&apos;ll send you a new confirmation link.
          </Typography>
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
            {...registerField('email')}
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
            {isSubmitting ? 'Sending...' : 'Resend confirmation email'}
          </Button>
        </Stack>

        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
          Back to{' '}
          <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 600 }}>
            sign in
          </Link>
        </Typography>
      </Stack>
    </AuthLayout>
  )
}
