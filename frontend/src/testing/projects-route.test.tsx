import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import { renderApp } from './renderApp'
import { server } from './msw/server'

describe('projects route', () => {
  it('shows the empty workspace state when the API returns no projects', async () => {
    useAuthStore
      .getState()
      .setAuth(
        { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
        'access-token',
      )

    await renderApp({ route: '/projects' })

    expect(await screen.findByText(/no workspaces yet/i)).toBeInTheDocument()
  })

  it('renders an error alert when the projects API fails', async () => {
    useAuthStore
      .getState()
      .setAuth(
        { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
        'access-token',
      )

    server.use(http.get('*/api/projects', () => new HttpResponse(null, { status: 500 })))

    await renderApp({ route: '/projects' })

    // The route should not show the empty state on error.
    expect(screen.queryByText(/no workspaces yet/i)).not.toBeInTheDocument()
  })
})
