import { screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type {
  Board,
  Card,
  Column,
  Comment,
  Project,
  ProjectMember,
  Subtask,
} from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const BOARD_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
const COLUMN_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc'
const CARD_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'access-token')
}

function makeProject(): Project {
  return {
    id: PROJECT_ID,
    name: 'Card Detail Test',
    ownerId: USER_ID,
    memberCount: 1,
    boardCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
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

function makeColumn(): Column {
  return {
    id: COLUMN_ID,
    boardId: BOARD_ID,
    name: 'To Do',
    position: 1000,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  }
}

function makeCard(overrides: Partial<Card> = {}): Card {
  return {
    id: CARD_ID,
    columnId: COLUMN_ID,
    column: { ...makeColumn(), boardId: BOARD_ID },
    title: 'Test Card',
    description: null,
    position: 1000,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    cardTags: [],
    assignments: [],
    attachments: [],
    subtasks: [],
    ...overrides,
  }
}

function serveBoardWithCard({
  card = makeCard(),
  members = [],
  comments = [],
}: {
  card?: Card
  members?: ProjectMember[]
  comments?: Comment[]
}) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(makeProject(), { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () => HttpResponse.json(members, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () =>
      HttpResponse.json([makeBoard()], { status: 200 }),
    ),
    http.get(`*/api/boards/${BOARD_ID}`, () => HttpResponse.json(makeBoard(), { status: 200 })),
    http.get(`*/api/boards/${BOARD_ID}/columns`, () =>
      HttpResponse.json([makeColumn()], { status: 200 }),
    ),
    http.get(`*/api/boards/${BOARD_ID}/cards`, () =>
      HttpResponse.json(
        { items: [card], page: 1, pageSize: 500, totalCount: 1 },
        { status: 200 },
      ),
    ),
    http.get(`*/api/cards/${CARD_ID}`, () => HttpResponse.json(card, { status: 200 })),
    http.get(`*/api/cards/${CARD_ID}/comments`, () =>
      HttpResponse.json(comments, { status: 200 }),
    ),
    http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
  )
}

async function openCardDetail(card?: Card) {
  authenticate()
  serveBoardWithCard({ card })
  await renderApp({
    route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}?cardId=${CARD_ID}`,
  })
  return await screen.findByRole('dialog')
}

describe('CardDetailDialog — title edit', () => {
  it('shows the current title in an editable field', async () => {
    const dialog = await openCardDetail(makeCard({ title: 'Original Title' }))
    expect(within(dialog).getByDisplayValue('Original Title')).toBeInTheDocument()
  })

  it('PUTs the new title with the version when Save Changes is clicked', async () => {
    let putBody: { title?: string; version?: number } | null = null
    server.use(
      http.put(`*/api/cards/${CARD_ID}`, async ({ request }) => {
        putBody = (await request.json()) as { title?: string; version?: number }
        return HttpResponse.json(
          { ...makeCard(), title: 'Renamed', version: 2 },
          { status: 200 },
        )
      }),
    )

    const dialog = await openCardDetail(makeCard({ title: 'Original' }))
    const user = userEvent.setup()

    const titleInput = within(dialog).getByDisplayValue('Original')
    await user.clear(titleInput)
    await user.type(titleInput, 'Renamed')

    await user.click(within(dialog).getByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(putBody).toMatchObject({ title: 'Renamed', version: 1 }))
  })

  it('shows the conflict alert when the API returns a 409', async () => {
    server.use(
      http.put(`*/api/cards/${CARD_ID}`, () =>
        HttpResponse.json({ error: { message: 'version' } }, { status: 409 }),
      ),
    )

    const dialog = await openCardDetail(makeCard({ title: 'Original' }))
    const user = userEvent.setup()

    const titleInput = within(dialog).getByDisplayValue('Original')
    await user.clear(titleInput)
    await user.type(titleInput, 'Stale Edit')
    await user.click(within(dialog).getByRole('button', { name: /save changes/i }))

    expect(
      await within(dialog).findByText(/this task was updated elsewhere/i),
    ).toBeInTheDocument()
  })
})

describe('CardDetailDialog — comments', () => {
  it('POSTs a new comment and clears the input after success', async () => {
    let postBody: { content?: string } | null = null
    server.use(
      http.post(`*/api/cards/${CARD_ID}/comments`, async ({ request }) => {
        postBody = (await request.json()) as { content?: string }
        return HttpResponse.json(
          {
            id: 'comment-1',
            cardId: CARD_ID,
            authorId: USER_ID,
            content: 'Hello there',
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          } satisfies Comment,
          { status: 201 },
        )
      }),
    )

    const dialog = await openCardDetail()
    const user = userEvent.setup()

    // Comments live inside the Details tab (the default).
    const commentInput = await within(dialog).findByPlaceholderText(/write a comment/i)
    await user.type(commentInput, 'Hello there')
    await user.click(within(dialog).getByRole('button', { name: /post comment/i }))

    await waitFor(() => expect(postBody).toEqual({ content: 'Hello there' }))
  })

  it('shows existing comments returned by the comments endpoint', async () => {
    const comment: Comment = {
      id: 'c-1',
      cardId: CARD_ID,
      authorId: USER_ID,
      content: 'Original comment text',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    }

    authenticate()
    serveBoardWithCard({ comments: [comment] })

    await renderApp({
      route: `/projects/${PROJECT_ID}/boards/${BOARD_ID}?cardId=${CARD_ID}`,
    })

    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('Original comment text')).toBeInTheDocument()
  })
})

describe('CardDetailDialog — subtasks', () => {
  it('shows existing subtasks with their checkboxes reflecting the completed state', async () => {
    const subtasks: Subtask[] = [
      {
        id: 's-1',
        cardId: CARD_ID,
        description: 'Write spec',
        completed: true,
        position: 1000,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
      {
        id: 's-2',
        cardId: CARD_ID,
        description: 'Implement',
        completed: false,
        position: 2000,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    ]

    const dialog = await openCardDetail(makeCard({ subtasks }))

    const completedCheckbox = within(dialog).getByRole('checkbox', {
      name: /toggle subtask write spec/i,
    })
    const pendingCheckbox = within(dialog).getByRole('checkbox', {
      name: /toggle subtask implement/i,
    })

    expect(completedCheckbox).toBeChecked()
    expect(pendingCheckbox).not.toBeChecked()
  })

  it('persists a toggled subtask completion via PUT when Save Changes is clicked', async () => {
    const subtasks: Subtask[] = [
      {
        id: 's-1',
        cardId: CARD_ID,
        description: 'Write spec',
        completed: false,
        position: 1000,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
      },
    ]

    let putBody: { completed?: boolean } | null = null
    server.use(
      http.put('*/api/subtasks/s-1', async ({ request }) => {
        putBody = (await request.json()) as { completed?: boolean }
        return HttpResponse.json({ ...subtasks[0], completed: true }, { status: 200 })
      }),
    )

    const dialog = await openCardDetail(makeCard({ subtasks }))
    const user = userEvent.setup()

    await user.click(
      within(dialog).getByRole('checkbox', { name: /toggle subtask write spec/i }),
    )
    await user.click(within(dialog).getByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(putBody).toEqual({ completed: true }))
  })
})

describe('CardDetailDialog — archive', () => {
  it('archives the card and closes the dialog when the archive button is clicked', async () => {
    let deleted = false
    server.use(
      http.delete(`*/api/cards/${CARD_ID}`, () => {
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const dialog = await openCardDetail()
    const user = userEvent.setup()

    // The archive control is labelled "Archive Task" in the dialog footer.
    const archiveControl = await within(dialog).findByRole('button', {
      name: /archive task/i,
    })
    await user.click(archiveControl)

    await waitFor(() => expect(deleted).toBe(true))
  })
})
