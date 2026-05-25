import { Link as RouterLink, useNavigate } from '@tanstack/react-router'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { login } from '@/features/auth/api/auth'
import { AuthLayout } from './AuthLayout'

const loginSchema = z.object({
  email: z.string().trim().email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

type LoginFormValues = z.infer<typeof loginSchema>

type LoginFormProps = {
  redirectTo?: string
}

export function LoginForm({ redirectTo }: LoginFormProps) {
  const navigate = useNavigate()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      await login(values)

      if (redirectTo && redirectTo.startsWith('/')) {
        navigate({ to: redirectTo as '/' })
        return
      }

      navigate({ to: '/projects' })
    } catch {
      setError('root.server', {
        type: 'server',
        message: 'Invalid credentials',
      })
    }
  })

  return (
    <AuthLayout>
      <Stack component="form" onSubmit={onSubmit} spacing={3} noValidate>
        <Stack spacing={0.5}>
          <Typography variant="h4" sx={{ fontWeight: 700 }}>
            Welcome back
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Sign in to your account to continue.
          </Typography>
        </Stack>

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
          {...register('email')}
        />

        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          fullWidth
          error={Boolean(errors.password)}
          helperText={errors.password?.message}
          {...register('password')}
        />

        <Link
          component={RouterLink}
          to="/reset-password"
          underline="hover"
          variant="body2"
          sx={{ fontWeight: 500, alignSelf: 'flex-end', mt: -1 }}
        >
          Forgot password?
        </Link>

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
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </Button>

        <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
          Don&apos;t have an account?{' '}
          <Link component={RouterLink} to="/register" underline="hover" sx={{ fontWeight: 600 }}>
            Sign up
          </Link>
        </Typography>
      </Stack>
    </AuthLayout>
  )
}
