import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Project, ProjectMember } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const OWNER_ID = '11111111-1111-1111-1111-111111111111'
const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'

function authenticate(userId = OWNER_ID) {
  useAuthStore.getState().setAuth({ id: userId, email: 'owner@example.com' }, 'access-token')
}

function makeArchivedProject(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Archived Workspace',
    ownerId: OWNER_ID,
    memberCount: 1,
    boardCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-04-30T00:00:00Z',
    deletedAt: '2026-05-01T00:00:00Z',
    ...overrides,
  }
}

function makeMember(userId: string): ProjectMember {
  return {
    userId,
    role: 0,
    joinedAt: '2026-01-01T00:00:00Z',
    email: `${userId}@example.com`,
    userName: `User ${userId.slice(0, 4)}`,
  }
}

describe('Archive page — permanent delete', () => {
  it('calls the purge endpoint when the owner clicks delete permanently on an archived project', async () => {
    authenticate()
    let purgeCalled = false

    server.use(
      http.get('*/api/projects/archived', () =>
        HttpResponse.json(
          { items: [makeArchivedProject()], page: 1, pageSize: 25, totalCount: 1 },
          { status: 200 },
        ),
      ),
      http.get('*/api/projects', () =>
        HttpResponse.json(
          { items: [], page: 1, pageSize: 25, totalCount: 0 },
          { status: 200 },
        ),
      ),
      http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
      http.get(/\/api\/cards\/archived(?:\?.*)?$/, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }, { status: 200 }),
      ),
      http.get(`*/api/projects/${PROJECT_ID}/members`, () =>
        HttpResponse.json([makeMember(OWNER_ID)], { status: 200 }),
      ),
      http.delete(`*/api/projects/${PROJECT_ID}/permanent`, () => {
        purgeCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/archive' })

    await screen.findByText('Archived Workspace')

    const deleteButton = await screen.findByRole('button', { name: /delete permanently/i })
    await user.click(deleteButton)

    await waitFor(() => expect(purgeCalled).toBe(true))
  })

  it('hides the delete button when the user cannot manage the workspace', async () => {
    const VIEWER_ID = '99999999-9999-9999-9999-999999999999'
    authenticate(VIEWER_ID)

    server.use(
      http.get('*/api/projects/archived', () =>
        HttpResponse.json(
          { items: [makeArchivedProject()], page: 1, pageSize: 25, totalCount: 1 },
          { status: 200 },
        ),
      ),
      http.get('*/api/projects', () =>
        HttpResponse.json(
          { items: [], page: 1, pageSize: 25, totalCount: 0 },
          { status: 200 },
        ),
      ),
      http.get('*/api/boards/archived', () => HttpResponse.json([], { status: 200 })),
      http.get(/\/api\/cards\/archived(?:\?.*)?$/, () =>
        HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 }, { status: 200 }),
      ),
      http.get(`*/api/projects/${PROJECT_ID}/members`, () =>
        HttpResponse.json([makeMember(OWNER_ID)], { status: 200 }),
      ),
    )

    await renderApp({ route: '/archive' })

    await screen.findByText('Archived Workspace')

    expect(screen.queryByRole('button', { name: /delete permanently/i })).not.toBeInTheDocument()
  })
})
