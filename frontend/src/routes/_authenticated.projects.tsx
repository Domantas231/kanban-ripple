import { Outlet, createFileRoute, useRouterState } from '@tanstack/react-router'
import { ProjectsListPage } from '@/features/projects'

export const Route = createFileRoute('/_authenticated/projects')({
  loader: () => ({ title: 'Workspaces' }),
  component: ProjectsRoute,
})

function ProjectsRoute() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  if (pathname !== '/projects') return <Outlet />
  return <ProjectsListPage />
}
