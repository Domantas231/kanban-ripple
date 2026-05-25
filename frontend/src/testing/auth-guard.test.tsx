import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const authResult = {
  userId: '11111111-1111-1111-1111-111111111111',
  email: 'user@example.com',
  accessToken: 'access-token',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  refreshToken: 'refresh-token',
  refreshTokenExpiresAt: '2030-01-08T00:00:00Z',
}

describe('auth guard — protected routes', () => {
  it('redirects unauthenticated visitors from a protected route to /login with the original href in ?redirect', async () => {
    server.use(
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
      ),
    )

    const { router } = await renderApp({ route: '/projects' })

    expect(await screen.findByRole('heading', { name: /welcome back/i })).toBeInTheDocument()

    const location = router.state.location
    expect(location.pathname).toBe('/login')
    expect((location.search as { redirect?: string }).redirect).toBe('/projects')
  })

  it('lets authenticated visitors through to a protected route without redirecting', async () => {
    useAuthStore
      .getState()
      .setAuth(
        { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
        'access-token',
      )

    const { router } = await renderApp({ route: '/projects' })

    // The empty-state placeholder rendered by ProjectsListPage.
    expect(await screen.findByText(/no workspaces yet/i)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/projects')
  })

  it('admits requests when the silent refresh succeeds (transient auth recovery)', async () => {
    // No in-memory access token, but /api/auth/refresh returns 200 — the
    // requireAuthenticated guard should restore the session and let us through.
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(authResult, { status: 200 })))

    const { router } = await renderApp({ route: '/projects' })

    expect(await screen.findByText(/no workspaces yet/i)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/projects')
  })
})

describe('auth guard — public routes', () => {
  it('redirects already-authenticated users away from /login to /projects', async () => {
    useAuthStore
      .getState()
      .setAuth(
        { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
        'access-token',
      )

    const { router } = await renderApp({ route: '/login' })

    // Should land on the workspaces page rather than render the login form.
    expect(await screen.findByText(/no workspaces yet/i)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/projects')
  })
})

describe('auth guard — login redirect=', () => {
  it('honors a relative ?redirect= path after a successful login', async () => {
    server.use(
      // beforeLoad of /login calls refresh; force it to fail so we render the form.
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
      ),
      http.post('*/api/auth/login', () => HttpResponse.json(authResult, { status: 200 })),
    )

    const user = userEvent.setup()
    const { router } = await renderApp({ route: '/login?redirect=%2Fprojects' })

    await user.type(await screen.findByLabelText(/email/i), 'user@example.com')
    await user.type(screen.getByLabelText(/password/i), 'pw')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText(/no workspaces yet/i)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/projects')
  })
})
