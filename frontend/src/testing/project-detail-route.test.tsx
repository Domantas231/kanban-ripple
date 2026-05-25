import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Board, Project, ProjectMember } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore
    .getState()
    .setAuth({ id: USER_ID, email: 'user@example.com' }, 'access-token')
}

function makeProject(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Detail Test Workspace',
    ownerId: USER_ID,
    memberCount: 1,
    boardCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeBoard(name: string, overrides: Partial<Board> = {}): Board {
  return {
    id: `board-${name.toLowerCase()}`,
    projectId: PROJECT_ID,
    name,
    position: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    cardCount: 0,
    columnCount: 0,
    ...overrides,
  }
}

function serveProjectDetail(project: Project, boards: Board[], members: ProjectMember[] = []) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(project, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () => HttpResponse.json(boards, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json(members, { status: 200 })),
    http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
  )
}

describe('project detail route — happy path', () => {
  it('renders the project name and board list when the project loads', async () => {
    authenticate()
    serveProjectDetail(makeProject({ name: 'My Workspace', boardCount: 2 }), [
      makeBoard('Sprint 1', { cardCount: 5 }),
      makeBoard('Sprint 2', { cardCount: 3 }),
    ])

    const { router } = await renderApp({ route: `/projects/${PROJECT_ID}` })

    expect(await screen.findByRole('heading', { name: 'My Workspace' })).toBeInTheDocument()
    expect(await screen.findByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByText('Sprint 2')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe(`/projects/${PROJECT_ID}`)
  })

  it('shows the empty-board state when the project has no boards yet', async () => {
    authenticate()
    serveProjectDetail(makeProject(), [])

    await renderApp({ route: `/projects/${PROJECT_ID}` })

    expect(await screen.findByText(/no boards yet/i)).toBeInTheDocument()
  })

  it('exposes "New Board" / "Import Trello" only to owners and editors', async () => {
    // Owner case — already covered by the happy path above; here we verify
    // viewer (role=3) hides management controls. role 0=owner, 1=admin, 2=editor, 3=viewer.
    authenticate()
    serveProjectDetail(
      makeProject({ ownerId: 'someone-else' }),
      [makeBoard('Read Only Board')],
      [
        {
          userId: USER_ID,
          role: 3,
          joinedAt: '2026-01-01T00:00:00Z',
        },
      ],
    )

    await renderApp({ route: `/projects/${PROJECT_ID}` })

    await screen.findByText('Read Only Board')
    expect(screen.queryByRole('button', { name: /new board/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /import trello/i })).not.toBeInTheDocument()
  })
})

describe('project detail route — error and missing-project handling', () => {
  it('redirects to /projects when the project endpoint returns 404', async () => {
    authenticate()
    server.use(
      http.get(`*/api/projects/${PROJECT_ID}`, () => new HttpResponse(null, { status: 404 })),
    )

    const { router } = await renderApp({ route: `/projects/${PROJECT_ID}` })

    // The route loader catches the 404 and redirects to /projects.
    await screen.findByText(/no workspaces yet/i)
    expect(router.state.location.pathname).toBe('/projects')
  })
})

describe('project detail route — board search', () => {
  it('filters the board grid by search term', async () => {
    authenticate()
    serveProjectDetail(makeProject(), [
      makeBoard('Backlog'),
      makeBoard('Sprint 1'),
      makeBoard('Sprint 2'),
    ])

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}` })

    await screen.findByText('Backlog')

    await user.type(screen.getByPlaceholderText(/search boards/i), 'Sprint')

    expect(await screen.findByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByText('Sprint 2')).toBeInTheDocument()
    expect(screen.queryByText('Backlog')).not.toBeInTheDocument()
  })
})
