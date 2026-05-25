import { Outlet, createRootRoute } from '@tanstack/react-router'
import { AppErrorBoundary } from '../components/errors/AppErrorBoundary'
import { GlobalApiFeedback } from '../components/feedback/GlobalApiFeedback'
import { FullPageSpinner } from '../components/loading/FullPageSpinner'

export const Route = createRootRoute({
  component: RootLayout,
  pendingComponent: RootPending,
})

function RootLayout() {
  return (
    <AppErrorBoundary>
      <main style={{ padding: '2rem' }}>
        <Outlet />
      </main>
      <GlobalApiFeedback />
    </AppErrorBoundary>
  )
}

function RootPending() {
  return <FullPageSpinner label="Loading app..." />
}
