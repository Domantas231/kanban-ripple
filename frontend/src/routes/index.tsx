import { createFileRoute, redirect } from '@tanstack/react-router'
import { isAuthenticated, refresh } from '@/features/auth'

export const Route = createFileRoute('/')({
  beforeLoad: async () => {
    if (isAuthenticated()) {
      throw redirect({ to: '/projects' })
    }

    const refreshed = await refresh()
      .then(() => true)
      .catch(() => false)

    throw redirect({ to: refreshed ? '/projects' : '/login' })
  },
})
