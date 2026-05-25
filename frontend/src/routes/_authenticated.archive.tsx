import { createFileRoute } from '@tanstack/react-router'
import { ArchivePage } from '@/features/archive'

export const Route = createFileRoute('/_authenticated/archive')({
  component: ArchivePage,
})
