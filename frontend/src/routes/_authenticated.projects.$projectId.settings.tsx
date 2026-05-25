import { createFileRoute } from '@tanstack/react-router'
import { ProjectSettingsPage } from '@/features/projects'

export const Route = createFileRoute('/_authenticated/projects/$projectId/settings')({
  loader: ({ params }) => ({ projectId: params.projectId }),
  component: ProjectSettingsRoute,
})

function ProjectSettingsRoute() {
  const { projectId } = Route.useLoaderData()
  return <ProjectSettingsPage projectId={projectId} />
}
