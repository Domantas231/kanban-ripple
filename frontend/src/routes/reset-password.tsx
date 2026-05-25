import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { PasswordResetForm } from '@/features/auth'
import { redirectIfAuthenticated } from '@/features/auth'

export const Route = createFileRoute('/reset-password')({
  beforeLoad: () => redirectIfAuthenticated(),
  validateSearch: z.object({
    token: z.string().optional(),
    email: z.string().optional(),
  }),
  component: ResetPasswordPage,
})

function ResetPasswordPage() {
  const { token, email } = Route.useSearch()
  return <PasswordResetForm token={token} encodedEmail={email} />
}
