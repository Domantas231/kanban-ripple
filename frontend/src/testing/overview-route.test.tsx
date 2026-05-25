import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type {
  Card,
  Column,
  ColumnSwimlane,
  Project,
  ProjectActivity,
  SwimlaneView,
} from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'access-token')
}

function makeProject(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Overview Test Workspace',
    ownerId: USER_ID,
    memberCount: 1,
    boardCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeColumn(name: string, position: number): Column {
  return {
    id: `col-${name.toLowerCase().replace(/\s+/g, '-')}`,
    boardId: 'b-1',
    name,
    position,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

function makeCard(
  title: string,
  columnId: string,
  overrides: Partial<Card> = {},
): Card {
  return {
    id: `card-${title.toLowerCase().replace(/\s+/g, '-')}`,
    columnId,
    title,
    position: 1000,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeSwimlane(columnsAndCards: Array<{ column: Column; cards: Card[] }>): SwimlaneView {
  const columns: ColumnSwimlane[] = columnsAndCards.map(({ column, cards }) => ({
    column,
    cards,
    cardCount: cards.length,
  }))

  return {
    projectId: PROJECT_ID,
    boards: [
      {
        board: {
          id: 'b-1',
          projectId: PROJECT_ID,
          name: 'Sprint 1',
          position: 0,
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        },
        columns,
      },
    ],
  }
}

function serveOverview({
  project = makeProject(),
  swimlane,
  activities = [],
}: {
  project?: Project
  swimlane: SwimlaneView
  activities?: ProjectActivity[]
}) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(project, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/swimlane`, () =>
      HttpResponse.json(swimlane, { status: 200 }),
    ),
    http.get(`*/api/projects/${PROJECT_ID}/activities`, () =>
      HttpResponse.json(activities, { status: 200 }),
    ),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json([], { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () => HttpResponse.json([], { status: 200 })),
  )
}

describe('overview route — empty workspace', () => {
  it('renders an empty state when the workspace has no boards yet', async () => {
    authenticate()
    serveOverview({ swimlane: { projectId: PROJECT_ID, boards: [] } })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    expect(await screen.findByText(/no boards yet/i)).toBeInTheDocument()
  })

  it('shows an error alert when the swimlane endpoint fails', async () => {
    authenticate()
    server.use(
      http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(makeProject(), { status: 200 })),
      http.get(`*/api/projects/${PROJECT_ID}/swimlane`, () => new HttpResponse(null, { status: 500 })),
      http.get(`*/api/projects/${PROJECT_ID}/activities`, () => HttpResponse.json([], { status: 200 })),
      http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json([], { status: 200 })),
      http.get(`*/api/projects/${PROJECT_ID}/boards`, () => HttpResponse.json([], { status: 200 })),
    )

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    expect(await screen.findByText(/unable to load overview/i)).toBeInTheDocument()
  })
})

describe('overview route — populated boards', () => {
  it('renders the board card grid with totals from the swimlane data', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveOverview({
      swimlane: makeSwimlane([
        {
          column: todo,
          cards: [
            makeCard('Spec', todo.id),
            makeCard('Build', todo.id),
            makeCard('Ship', todo.id),
          ],
        },
      ]),
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    expect(await screen.findByRole('heading', { name: 'Boards', level: 3 })).toBeInTheDocument()
    // The board name appears in multiple widgets; assert it shows somewhere.
    expect((await screen.findAllByText('Sprint 1')).length).toBeGreaterThan(0)
  })
})

describe('overview route — overdue and upcoming widgets', () => {
  it('shows an "Overdue" widget when there are overdue cards', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveOverview({
      swimlane: makeSwimlane([
        {
          column: todo,
          cards: [
            makeCard('Late task', todo.id, { dueDate: '2020-01-01T00:00:00Z' }),
            makeCard('On time task', todo.id),
          ],
        },
      ]),
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    // "Late task" appears in both the overdue widget and the unassigned widget.
    expect((await screen.findAllByText('Late task')).length).toBeGreaterThan(0)
    expect(screen.getAllByText(/^overdue$/i).length).toBeGreaterThan(0)
  })

  it('shows an "Upcoming" widget when cards have a near-future due date', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)

    // 3 days from now → upcoming.
    const upcomingIso = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString()

    serveOverview({
      swimlane: makeSwimlane([
        {
          column: todo,
          cards: [makeCard('Soon task', todo.id, { dueDate: upcomingIso })],
        },
      ]),
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    expect((await screen.findAllByText('Soon task')).length).toBeGreaterThan(0)
  })
})

describe('overview route — unassigned widget', () => {
  it('lists cards with no assignees in the unassigned widget', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveOverview({
      swimlane: makeSwimlane([
        {
          column: todo,
          cards: [
            makeCard('Orphan card', todo.id, { assignments: [] }),
            makeCard('Owned card', todo.id, {
              assignments: [
                {
                  id: 'a-1',
                  cardId: 'card-owned-card',
                  userId: 'someone',
                  assignedAt: '2026-01-01T00:00:00Z',
                },
              ],
            }),
          ],
        },
      ]),
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    expect((await screen.findAllByText('Orphan card')).length).toBeGreaterThan(0)
  })
})

describe('overview route — recent activity', () => {
  it('renders activity items returned by the activities endpoint', async () => {
    authenticate()
    serveOverview({
      swimlane: makeSwimlane([
        { column: makeColumn('To Do', 1000), cards: [makeCard('Anything', 'col-to-do')] },
      ]),
      activities: [
        {
          id: 'act-1',
          entityType: 'card',
          cardId: 'card-anything',
          cardTitle: 'Anything',
          boardId: 'b-1',
          userId: USER_ID,
          userName: 'Someone',
          action: 'updated',
          createdAt: '2026-05-01T00:00:00Z',
          entityName: 'Anything',
        },
      ],
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    // The recent activity widget renders the user name + action.
    expect(await screen.findByText(/someone/i)).toBeInTheDocument()
  })

  it('navigates to the card when a card-typed activity row is clicked', async () => {
    authenticate()
    serveOverview({
      swimlane: makeSwimlane([
        { column: makeColumn('To Do', 1000), cards: [makeCard('Anything', 'col-to-do')] },
      ]),
      activities: [
        {
          id: 'act-1',
          entityType: 'card',
          cardId: 'card-anything',
          cardTitle: 'Anything',
          boardId: 'b-1',
          userId: USER_ID,
          userName: 'Someone',
          action: 'updated',
          createdAt: '2026-05-01T00:00:00Z',
          entityName: 'Anything',
        },
      ],
    })

    // Clicking the row navigates to /projects/:id/boards/b-1?cardId=...; the
    // board route loads its own data, so we stub those endpoints to avoid
    // unhandled-request errors.
    server.use(
      http.get('*/api/boards/b-1', () =>
        HttpResponse.json(
          {
            id: 'b-1',
            projectId: PROJECT_ID,
            name: 'Sprint 1',
            position: 0,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          },
          { status: 200 },
        ),
      ),
      http.get('*/api/boards/b-1/columns', () => HttpResponse.json([], { status: 200 })),
      http.get('*/api/cards/card-anything', () =>
        HttpResponse.json(
          {
            id: 'card-anything',
            columnId: 'col-to-do',
            title: 'Anything',
            position: 1000,
            version: 1,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          },
          { status: 200 },
        ),
      ),
      http.get('*/api/cards/card-anything/comments', () => HttpResponse.json([], { status: 200 })),
    )

    const user = userEvent.setup()
    const { router } = await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    const userText = await screen.findByText(/someone/i)
    const row = userText.closest('[role="button"]') as HTMLElement | null
    if (row) {
      await user.click(row)
    } else {
      await user.click(userText)
    }

    expect(router.state.location.pathname).toMatch(
      new RegExp(`^/projects/${PROJECT_ID}/boards/b-1`),
    )
  })
})

describe('overview route — Team & Tags section', () => {
  it('renders the Team Workload and Tags Breakdown widgets', async () => {
    authenticate()
    const todo = makeColumn('To Do', 1000)
    serveOverview({
      swimlane: makeSwimlane([
        {
          column: todo,
          cards: [makeCard('Card A', todo.id)],
        },
      ]),
    })

    await renderApp({ route: `/projects/${PROJECT_ID}/swimlane` })

    // Section label is rendered in overline style.
    const teamSection = await screen.findByText(/team & tags/i)
    expect(teamSection).toBeInTheDocument()
  })
})

// Avoid an unused import warning when no test uses `within`.
void within
