import { createFileRoute } from '@tanstack/react-router'
import { ProtectedLayout } from '@/app/layout/ProtectedLayout'
import { requireAuthenticated } from '@/features/auth'

export const Route = createFileRoute('/_authenticated')({
  beforeLoad: ({ location }) => requireAuthenticated(location.href),
  component: ProtectedLayout,
})
