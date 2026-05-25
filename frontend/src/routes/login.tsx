import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { LoginForm } from '@/features/auth'
import { redirectIfAuthenticated } from '@/features/auth'

export const Route = createFileRoute('/login')({
  beforeLoad: ({ search }) => redirectIfAuthenticated((search as { redirect?: string }).redirect),
  validateSearch: z.object({
    redirect: z.string().optional(),
  }),
  component: LoginPage,
})

function LoginPage() {
  const { redirect } = Route.useSearch()
  return <LoginForm redirectTo={redirect} />
}
