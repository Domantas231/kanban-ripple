import { createFileRoute } from '@tanstack/react-router'
import { UserSettingsPage } from '@/features/settings'

export const Route = createFileRoute('/_authenticated/settings')({
  component: UserSettingsPage,
})
