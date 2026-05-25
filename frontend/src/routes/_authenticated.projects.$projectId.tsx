import { createFileRoute, redirect } from '@tanstack/react-router'
import { getProject } from '@/features/projects'
import { ProjectDetailPage } from '@/features/projects'

export const Route = createFileRoute('/_authenticated/projects/$projectId')({
  loader: async ({ params }) => {
    try {
      await getProject(params.projectId)
    } catch {
      throw redirect({ to: '/projects' })
    }
    return { projectId: params.projectId }
  },
  component: ProjectDetailRoute,
})

function ProjectDetailRoute() {
  const { projectId } = Route.useLoaderData()
  return <ProjectDetailPage projectId={projectId} />
}
