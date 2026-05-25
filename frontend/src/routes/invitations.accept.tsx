import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { refresh, isAuthenticated } from '@/features/auth'
import { InvitationAcceptPage } from '@/features/invitations'

export const Route = createFileRoute('/invitations/accept')({
  validateSearch: z.object({
    token: z.string().optional(),
  }),
  beforeLoad: async () => {
    if (isAuthenticated()) return
    try {
      await refresh()
    } catch {
      // Not authenticated: page will show sign-in prompt
    }
  },
  component: InvitationAcceptRoute,
})

function InvitationAcceptRoute() {
  const { token } = Route.useSearch()
  return <InvitationAcceptPage token={token} />
}
