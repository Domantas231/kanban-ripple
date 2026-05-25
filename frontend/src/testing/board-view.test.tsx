import { screen, within } from '@testing-library/react'
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
    name: 'Board Test Workspace',
    ownerId: USER_ID,
    memberCount: 1,
    boardCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeBoard(overrides: Partial<Board> = {}): Board {
  return {
    id: BOARD_ID,
    projectId: PROJECT_ID,
    name: 'Test Board',
    position: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeColumn(name: string, position: number, overrides: Partial<Column> = {}): Column {
  return {
    id: `column-${name.toLowerCase().replace(/\s+/g, '-')}`,
    boardId: BOARD_ID,
    name,
    position,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeCard(title: string, columnId: string, position: number, overrides: Partial<Card> = {}): Card {
  return {
    id: `card-${title.toLowerCase().replace(/\s+/g, '-')}`,
    columnId,
    title,
    position,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function serveBoardView({
  project = makeProject(),
  board = makeBoard(),
  columns,
  cards,
  members = [],
}: {
  project?: Project
  board?: Board
  columns: Column[]
  cards: Card[]
  members?: ProjectMember[]
}) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(project, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json(members, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () => HttpResponse.json([board], { status: 200 })),
    http.get(`*/api/boards/${BOARD_ID}`, () => HttpResponse.json(board, { status: 200 })),
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

describe('board view — initial render', () => {
  it('renders every column and groups cards under their owning column', async () => {
    authenticate()

    const todoColumn = makeColumn('To Do', 1000)
    const inProgressColumn = makeColumn('In Progress', 2000)
    const doneColumn = makeColumn('Done', 3000)

    serveBoardView({
      columns: [todoColumn, inProgressColumn, doneColumn],
      cards: [
        makeCard('Spec the migration', todoColumn.id, 1000),
        makeCard('Build the migration runner', inProgressColumn.id, 1000),
        makeCard('Ship the v1 launch', doneColumn.id, 1000),
      ],
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    expect(await screen.findByText('To Do')).toBeInTheDocument()
    expect(screen.getByText('In Progress')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()

    expect(await screen.findByText('Spec the migration')).toBeInTheDocument()
    expect(screen.getByText('Build the migration runner')).toBeInTheDocument()
    expect(screen.getByText('Ship the v1 launch')).toBeInTheDocument()
  })

  it('orders cards within a column by position ascending', async () => {
    authenticate()
    const column = makeColumn('Backlog', 1000)
    serveBoardView({
      columns: [column],
      cards: [
        makeCard('Third', column.id, 3000),
        makeCard('First', column.id, 1000),
        makeCard('Second', column.id, 2000),
      ],
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    // Wait for the cards to load.
    await screen.findByText('First')

    const titles = screen.getAllByText(/^(First|Second|Third)$/).map((node) => node.textContent)
    expect(titles).toEqual(['First', 'Second', 'Third'])
  })
})

describe('board view — empty states', () => {
  it('shows the board chrome even when there are no columns', async () => {
    authenticate()
    serveBoardView({ columns: [], cards: [] })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    // The board name renders as a heading even when the board is empty.
    expect(await screen.findByText('Test Board')).toBeInTheDocument()
  })
})

describe('board view — permissions', () => {
  it('hides the "Add list" affordance for viewers', async () => {
    authenticate()
    const column = makeColumn('Backlog', 1000)

    serveBoardView({
      project: makeProject({ ownerId: 'someone-else' }),
      columns: [column],
      cards: [makeCard('Read only card', column.id, 1000)],
      members: [{ userId: USER_ID, role: 3, joinedAt: '2026-01-01T00:00:00Z' }],
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    await screen.findByText('Read only card')

    // "Add list" only renders for editors+. Viewers should never see it.
    expect(screen.queryByText(/^Add list$/)).not.toBeInTheDocument()
  })

  it('shows the "Add list" affordance for owners', async () => {
    authenticate()
    serveBoardView({ columns: [], cards: [] })

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    expect(await screen.findByText(/^Add list$/)).toBeInTheDocument()
  })
})

describe('board view — error handling', () => {
  it('keeps the board chrome rendered even if cards fail to load', async () => {
    authenticate()
    server.use(
      http.get(`*/api/projects/${PROJECT_ID}`, () =>
        HttpResponse.json(makeProject(), { status: 200 }),
      ),
      http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json([], { status: 200 })),
      http.get(`*/api/projects/${PROJECT_ID}/boards`, () =>
        HttpResponse.json([makeBoard()], { status: 200 }),
      ),
      http.get(`*/api/boards/${BOARD_ID}`, () => HttpResponse.json(makeBoard(), { status: 200 })),
      http.get(`*/api/boards/${BOARD_ID}/columns`, () =>
        HttpResponse.json([makeColumn('Only column', 1000)], { status: 200 }),
      ),
      http.get(`*/api/boards/${BOARD_ID}/cards`, () => new HttpResponse(null, { status: 500 })),
      http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
    )

    await renderApp({ route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}` })

    // The column header and board header still render even with a 500.
    expect(await screen.findByText('Only column')).toBeInTheDocument()
  })
})

describe('board view — preselected card via search param', () => {
  it('opens the card detail dialog when ?cardId points at an existing card', async () => {
    authenticate()
    const column = makeColumn('To Do', 1000)
    const card = makeCard('Preselected', column.id, 1000)

    serveBoardView({ columns: [column], cards: [card] })

    // The detail dialog also fetches the card itself plus comments. Without
    // these stubs the global "error on unhandled" strategy fires.
    server.use(
      http.get(`*/api/cards/${card.id}`, () => HttpResponse.json(card, { status: 200 })),
      http.get(`*/api/cards/${card.id}/comments`, () => HttpResponse.json([], { status: 200 })),
    )

    await renderApp({
      route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}?cardId=${card.id}`,
    })

    const dialog = await screen.findByRole('dialog')
    // The title is rendered as an editable TextField — assert by display value.
    expect(within(dialog).getByDisplayValue('Preselected')).toBeInTheDocument()
  })
})
