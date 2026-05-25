import { createRouter } from '@tanstack/react-router'
import { NotFoundPage } from '@/components/errors/NotFoundPage'
import { FullPageSpinner } from '@/components/loading/FullPageSpinner'
import { routeTree } from './routeTree.gen'

export const router = createRouter({
  routeTree,
  defaultPendingComponent: () => <FullPageSpinner />,
  defaultNotFoundComponent: NotFoundPage,
  defaultPendingMs: 0,
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
