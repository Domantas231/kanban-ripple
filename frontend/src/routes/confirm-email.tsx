import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { ConfirmEmailForm } from '@/features/auth'

export const Route = createFileRoute('/confirm-email')({
  validateSearch: z.object({
    token: z.string().optional(),
    email: z.string().optional(),
  }),
  component: ConfirmEmailPage,
})

function ConfirmEmailPage() {
  const search = Route.useSearch()

  return <ConfirmEmailForm token={search.token} encodedEmail={search.email} />
}
