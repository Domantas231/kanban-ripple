import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from '@tanstack/react-router'
import '../index.css'
import { configureApiClientAuth, configureApiClientNavigation } from '@/lib/api-client'
import { useAuthStore } from '@/features/auth'
import { initializeSignalRConnectionLifecycle } from '@/features/realtime'
import { AppProvider } from './provider'
import { router } from './router'

configureApiClientAuth({
  getAccessToken: () => useAuthStore.getState().accessToken,
  applyRefreshedSession: (user, accessToken) => useAuthStore.getState().setAuth(user, accessToken),
  clearSession: () => useAuthStore.getState().clearAuth(),
})

configureApiClientNavigation({
  redirectToWorkspaces: () => {
    if (router.state.location.pathname !== '/projects') {
      void router.navigate({ to: '/projects' })
    }
  },
})

initializeSignalRConnectionLifecycle()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProvider>
      <RouterProvider router={router} />
    </AppProvider>
  </StrictMode>,
)
