import { redirect } from '@tanstack/react-router'
import { refresh } from '../api/auth'
import { useAuthStore } from '../stores/authStore'

export function isAuthenticated(): boolean {
  return useAuthStore.getState().isAuthenticated
}

export async function redirectIfAuthenticated(redirectTo: string = '/projects'): Promise<void> {
  if (isAuthenticated()) {
    throw redirect({ to: redirectTo })
  }

  const refreshed = await refresh()
    .then(() => true)
    .catch(() => false)

  if (refreshed) {
    throw redirect({ to: redirectTo })
  }
}

export async function requireAuthenticated(currentHref: string): Promise<void> {
  if (isAuthenticated()) {
    return
  }

  try {
    await refresh()
  } catch {
    throw redirect({
      to: '/login',
      search: { redirect: currentHref },
    })
  }
}
