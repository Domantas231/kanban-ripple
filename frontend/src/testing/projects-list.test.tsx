import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Project } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

function makeProject(name: string, overrides: Partial<Project> = {}): Project {
  const id = `00000000-0000-0000-0000-${name.padEnd(12, 'x').slice(0, 12)}`
  return {
    id,
    name,
    ownerId: '11111111-1111-1111-1111-111111111111',
    memberCount: 1,
    boardCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function authenticate() {
  useAuthStore
    .getState()
    .setAuth(
      { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
      'access-token',
    )
}

function serveProjects(items: Project[]) {
  server.use(
    http.get('*/api/projects', () =>
      HttpResponse.json(
        { items, page: 1, pageSize: 25, totalCount: items.length },
        { status: 200 },
      ),
    ),
  )
}

describe('projects list — populated', () => {
  it('renders each project from the API', async () => {
    authenticate()
    serveProjects([makeProject('Alpha'), makeProject('Beta'), makeProject('Gamma')])

    await renderApp({ route: '/projects' })

    expect(await screen.findByText('Alpha')).toBeInTheDocument()
    expect(screen.getByText('Beta')).toBeInTheDocument()
    expect(screen.getByText('Gamma')).toBeInTheDocument()
  })

  it('filters by search query and shows the empty-search state when nothing matches', async () => {
    authenticate()
    serveProjects([makeProject('Alpha'), makeProject('Beta'), makeProject('Gamma')])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await screen.findByText('Alpha')

    await user.type(screen.getByPlaceholderText(/search workspaces/i), 'gam')

    expect(await screen.findByText('Gamma')).toBeInTheDocument()
    expect(screen.queryByText('Alpha')).not.toBeInTheDocument()
    expect(screen.queryByText('Beta')).not.toBeInTheDocument()

    // Now type a query that matches none of them.
    await user.clear(screen.getByPlaceholderText(/search workspaces/i))
    await user.type(screen.getByPlaceholderText(/search workspaces/i), 'zzz')

    expect(await screen.findByText(/no workspaces matching/i)).toBeInTheDocument()
  })

  it('changes sort order via the sort menu', async () => {
    authenticate()
    serveProjects([
      makeProject('Banana', { updatedAt: '2026-03-01T00:00:00Z', boardCount: 1 }),
      makeProject('Apple', { updatedAt: '2026-01-01T00:00:00Z', boardCount: 5 }),
      makeProject('Cherry', { updatedAt: '2026-02-01T00:00:00Z', boardCount: 2 }),
    ])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    // Default sort is "Last updated" descending → Banana, Cherry, Apple.
    await screen.findByText('Banana')
    const initialOrder = screen.getAllByRole('heading', { level: 6 }).map((el) => el.textContent)
    expect(initialOrder).toEqual(['Banana', 'Cherry', 'Apple'])

    // Switch sort to "Name". For string columns, the default direction
    // (sortAsc=false) is alphabetical ascending because localeCompare returns
    // a positive number when a > b.
    await user.click(screen.getByRole('button', { name: /sort workspaces/i }))
    await user.click(await screen.findByRole('menuitem', { name: /name/i }))

    const byName = screen.getAllByRole('heading', { level: 6 }).map((el) => el.textContent)
    expect(byName).toEqual(['Apple', 'Banana', 'Cherry'])

    // Click Name again to flip the direction.
    await user.click(screen.getByRole('button', { name: /sort workspaces/i }))
    await user.click(await screen.findByRole('menuitem', { name: /name/i }))

    const byNameFlipped = screen.getAllByRole('heading', { level: 6 }).map((el) => el.textContent)
    expect(byNameFlipped).toEqual(['Cherry', 'Banana', 'Apple'])
  })

  it('paginates when there are more projects than fit on one page', async () => {
    authenticate()
    // PAGE_SIZE in ProjectsListPage is 9.
    const items = Array.from({ length: 12 }, (_, i) =>
      makeProject(`Project ${String(i + 1).padStart(2, '0')}`, {
        updatedAt: `2026-01-${String(i + 1).padStart(2, '0')}T00:00:00Z`,
      }),
    )
    serveProjects(items)

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await screen.findByText('Project 12')
    // Default sort = updated desc → newest 9 (12..04) are on page 1.
    expect(screen.getByText('Project 04')).toBeInTheDocument()
    expect(screen.queryByText('Project 03')).not.toBeInTheDocument()

    // Advance to page 2.
    const pagination = screen.getByRole('navigation', { name: /pagination/i })
    await user.click(within(pagination).getByRole('button', { name: /go to page 2/i }))

    expect(await screen.findByText('Project 03')).toBeInTheDocument()
    expect(screen.queryByText('Project 12')).not.toBeInTheDocument()
  })
})
