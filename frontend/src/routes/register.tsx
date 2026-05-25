import { createFileRoute } from '@tanstack/react-router'
import { RegistrationForm } from '@/features/auth'
import { redirectIfAuthenticated } from '@/features/auth'

export const Route = createFileRoute('/register')({
  beforeLoad: () => redirectIfAuthenticated(),
  component: RegistrationForm,
})
