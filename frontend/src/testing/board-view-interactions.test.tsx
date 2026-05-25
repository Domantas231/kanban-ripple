import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Board, Card, Column, Project, ProjectMember } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const BOARD_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'access-token')
}

function makeProject(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Board Interactions Test',
    ownerId: USER_ID,
    memberCount: 1,
    boardCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeBoard(): Board {
  return {
    id: BOARD_ID,
    projectId: PROJECT_ID,
    name: 'Test Board',
    position: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

function makeColumn(name: string, position: number): Column {
  return {
    id: `col-${name.toLowerCase().replace(/\s+/g, '-')}`,
    boardId: BOARD_ID,
    name,
    position,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

function makeCard(title: string, columnId: string, position = 1000): Card {
  return {
    id: `card-${title.toLowerCase().replace(/\s+/g, '-')}`,
    columnId,
    title,
    position,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

function serveBoard({
  project = makeProject(),
  columns,
  cards,
  members = [],
}: {
  project?: Project
  columns: Column[]
  cards: Card[]
  members?: ProjectMember[]
}) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(project, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () =>
      HttpResponse.json(members, { status: 200 }),
    ),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () =>
      HttpResponse.json([makeBoard()], { status: 200 }),
    ),
    http.get(`*/api/boards/${BOARD_ID}`, () => HttpResponse.json(makeBoard(), { status: 200 })),
    http.get(`*/api/boards/${BOARD_ID}/columns`, () => HttpResponse.json(columns, { status: 200 })),
    http.get(`*/api/boards/${BOARD_ID}/cards`, () =>
      HttpResponse.json(
        { items: cards, page: 1, pageSize: 500, totalCount: cards.length },
        { status: 200 },
      ),
    ),
    http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
  )
}

describe('board view — inline card creation', () => {
  it('reveals the inline title input when "Add a task" is clicked', async () => {
    authenticate()
    serveBoard({ columns: [makeColumn('To Do', 1000)], cards: [] })

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByRole('button', { name: /add a task/i }))

    expect(await screen.findByPlaceholderText(/enter a title/i)).toBeInTheDocument()
  })

  it('POSTs the new card to /api/columns/:id/cards when the user presses Enter', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveBoard({ columns: [todo], cards: [] })

    let postBody: { title?: string } | null = null
    server.use(
      http.post(`*/api/columns/${todo.id}/cards`, async ({ request }) => {
        postBody = (await request.json()) as { title?: string }
        return HttpResponse.json(makeCard('New card', todo.id), { status: 201 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByRole('button', { name: /add a task/i }))
    const input = await screen.findByPlaceholderText(/enter a title/i)
    await user.type(input, 'New card{Enter}')

    await waitFor(() => expect(postBody).toMatchObject({ title: 'New card' }))
  })

  it('does not POST when the input is empty and Enter is pressed', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveBoard({ columns: [todo], cards: [] })

    let posts = 0
    server.use(
      http.post(`*/api/columns/${todo.id}/cards`, () => {
        posts += 1
        return HttpResponse.json(makeCard('Whatever', todo.id), { status: 201 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByRole('button', { name: /add a task/i }))
    const input = await screen.findByPlaceholderText(/enter a title/i)
    await user.type(input, '{Enter}')

    // Give the (non-)mutation a moment.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(posts).toBe(0)
  })
})

describe('board view — column creation', () => {
  it('expands the inline list-name input when "Add list" is clicked', async () => {
    authenticate()
    serveBoard({ columns: [], cards: [] })

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByText(/^Add list$/))

    expect(await screen.findByPlaceholderText(/list name/i)).toBeInTheDocument()
  })

  it('POSTs the new column when the user presses Enter inside the input', async () => {
    authenticate()
    serveBoard({ columns: [], cards: [] })

    let postBody: { name?: string } | null = null
    server.use(
      http.post(`*/api/boards/${BOARD_ID}/columns`, async ({ request }) => {
        postBody = (await request.json()) as { name?: string }
        return HttpResponse.json(makeColumn('Backlog', 1000), { status: 201 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByText(/^Add list$/))
    const input = await screen.findByPlaceholderText(/list name/i)
    await user.type(input, 'Backlog{Enter}')

    await waitFor(() => expect(postBody).toEqual({ name: 'Backlog' }))
  })

  it('cancels the inline input when Escape is pressed', async () => {
    authenticate()
    serveBoard({ columns: [], cards: [] })

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await user.click(await screen.findByText(/^Add list$/))
    const input = await screen.findByPlaceholderText(/list name/i)
    await user.type(input, 'Spike{Escape}')

    await waitFor(() => expect(screen.queryByPlaceholderText(/list name/i)).not.toBeInTheDocument())
  })
})

describe('board view — column rename', () => {
  it('PUTs the renamed column when the title field is edited and Enter is pressed', async () => {
    authenticate()
    const original = makeColumn('Backlog', 1000)
    serveBoard({ columns: [original], cards: [] })

    let putBody: { name?: string } | null = null
    server.use(
      http.put(`*/api/columns/${original.id}`, async ({ request }) => {
        putBody = (await request.json()) as { name?: string }
        return HttpResponse.json({ ...original, name: 'Refined' }, { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    // The column title is a Typography until clicked; click to enter edit mode.
    await user.click(await screen.findByText('Backlog'))
    const nameInput = (await screen.findByDisplayValue('Backlog')) as HTMLInputElement
    await user.clear(nameInput)
    await user.type(nameInput, 'Refined{Enter}')

    await waitFor(() => expect(putBody).toEqual({ name: 'Refined' }))
  })
})

describe('board view — board name rename', () => {
  it('PUTs the new board name on Enter', async () => {
    authenticate()
    serveBoard({ columns: [makeColumn('To Do', 1000)], cards: [] })

    let putBody: { name?: string; position?: number } | null = null
    server.use(
      http.put(`*/api/boards/${BOARD_ID}`, async ({ request }) => {
        putBody = (await request.json()) as { name?: string; position?: number }
        return HttpResponse.json({ ...makeBoard(), name: 'Renamed Board' }, { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    // The board title is a Typography until clicked. Click then edit.
    await user.click(await screen.findByText('Test Board'))
    const nameInput = (await screen.findByDisplayValue('Test Board')) as HTMLInputElement
    await user.clear(nameInput)
    await user.type(nameInput, 'Renamed Board{Enter}')

    await waitFor(() => expect(putBody).toMatchObject({ name: 'Renamed Board' }))
  })
})

describe('board view — viewer permissions', () => {
  it('hides "Add a task" for viewers (role 3)', async () => {
    authenticate()
    serveBoard({
      project: makeProject({ ownerId: 'someone-else' }),
      columns: [makeColumn('To Do', 1000)],
      cards: [makeCard('Read only card', 'col-to-do')],
      members: [{ userId: USER_ID, role: 3, joinedAt: '2026-01-01T00:00:00Z' }],
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await screen.findByText('Read only card')
    expect(screen.queryByRole('button', { name: /add a task/i })).not.toBeInTheDocument()
  })
})

void within
