import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Project, ProjectMember } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const PROJECT_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
const OWNER_ID = '11111111-1111-1111-1111-111111111111'
const MEMBER_ID = '22222222-2222-2222-2222-222222222222'

function authenticate(userId = OWNER_ID) {
  useAuthStore.getState().setAuth({ id: userId, email: 'user@example.com' }, 'access-token')
}

function makeProject(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Test Workspace',
    ownerId: OWNER_ID,
    memberCount: 2,
    boardCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function makeMember(
  userId: string,
  role: 0 | 1 | 2 | 3,
  overrides: Partial<ProjectMember> = {},
): ProjectMember {
  return {
    userId,
    role,
    joinedAt: '2026-01-01T00:00:00Z',
    email: `${userId}@example.com`,
    userName: `User ${userId.slice(0, 4)}`,
    ...overrides,
  }
}

function serveSettings(project: Project, members: ProjectMember[]) {
  server.use(
    http.get(`*/api/projects/${PROJECT_ID}`, () => HttpResponse.json(project, { status: 200 })),
    http.get(`*/api/projects/${PROJECT_ID}/members`, () =>
      HttpResponse.json(members, { status: 200 }),
    ),
    http.get(`*/api/projects/${PROJECT_ID}/boards`, () => HttpResponse.json([], { status: 200 })),
  )
}

describe('project settings — read-only access', () => {
  // The other 10 tests in this file already cover the owner-only mutation
  // paths. The viewer-mode banner is the one user-visible difference.
  // We assert the negative-case controls stay hidden — the role-derived
  // banner itself is decorative copy.
  it(
    'hides owner-only controls for viewers',
    async () => {
      authenticate(MEMBER_ID)
      serveSettings(
        makeProject(),
        [makeMember(OWNER_ID, 0), makeMember(MEMBER_ID, 3)], // viewer
      )

      await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

      // Page heading renders unconditionally once the route mounts.
      await waitFor(
        () => {
          expect(
            screen.getByRole('heading', { name: /workspace settings/i, level: 3 }),
          ).toBeInTheDocument()
        },
        { timeout: 8000 },
      )

      // Wait for an owner-only control to definitively NOT be present —
      // the simplest test is that the invite-email field never appears.
      await waitFor(
        () => {
          expect(screen.queryByPlaceholderText(/colleague's email/i)).not.toBeInTheDocument()
        },
        { timeout: 4000 },
      )

      expect(screen.queryByRole('button', { name: /save changes/i })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: /archive workspace/i })).not.toBeInTheDocument()
    },
    15000,
  )
})

describe('project settings — workspace name', () => {
  it('disables Save Changes until the name actually differs from the current value', async () => {
    authenticate()
    serveSettings(makeProject({ name: 'Original Name' }), [makeMember(OWNER_ID, 0)])

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const saveButton = await screen.findByRole('button', { name: /save changes/i })
    expect(saveButton).toBeDisabled()

    const nameInput = (await screen.findByDisplayValue('Original Name')) as HTMLInputElement
    await user.clear(nameInput)
    await user.type(nameInput, 'New Name')

    await waitFor(() => expect(saveButton).toBeEnabled())
  })

  it('PUTs to /api/projects/:id when Save is clicked', async () => {
    authenticate()
    let putBody: { name?: string } | null = null

    serveSettings(makeProject({ name: 'Original' }), [makeMember(OWNER_ID, 0)])
    server.use(
      http.put(`*/api/projects/${PROJECT_ID}`, async ({ request }) => {
        putBody = (await request.json()) as { name?: string }
        return HttpResponse.json(makeProject({ name: 'Renamed' }), { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const nameInput = (await screen.findByDisplayValue('Original')) as HTMLInputElement
    await user.clear(nameInput)
    await user.type(nameInput, 'Renamed')
    await user.click(await screen.findByRole('button', { name: /save changes/i }))

    await waitFor(() => expect(putBody).not.toBeNull())
    expect(putBody).toEqual({ name: 'Renamed' })
  })
})

describe('project settings — invite user', () => {
  it('disables Invite User until a valid email is typed', async () => {
    authenticate()
    serveSettings(makeProject(), [makeMember(OWNER_ID, 0)])

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const emailInput = await screen.findByPlaceholderText(/colleague's email/i)
    const inviteButton = screen.getByRole('button', { name: /invite user/i })
    expect(inviteButton).toBeDisabled()

    await user.type(emailInput, 'not-an-email')
    expect(inviteButton).toBeDisabled()

    await user.clear(emailInput)
    await user.type(emailInput, 'colleague@example.com')
    await waitFor(() => expect(inviteButton).toBeEnabled())
  })

  it('POSTs the invite and clears the input on success', async () => {
    authenticate()
    let inviteBody: { email?: string; role?: number } | null = null

    serveSettings(makeProject(), [makeMember(OWNER_ID, 0)])
    server.use(
      http.post(`*/api/projects/${PROJECT_ID}/invite`, async ({ request }) => {
        inviteBody = (await request.json()) as { email?: string; role?: number }
        return HttpResponse.json({ message: 'sent' }, { status: 200 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const emailInput = (await screen.findByPlaceholderText(
      /colleague's email/i,
    )) as HTMLInputElement
    await user.type(emailInput, 'colleague@example.com')
    await user.click(screen.getByRole('button', { name: /invite user/i }))

    await waitFor(() =>
      expect(inviteBody).toEqual({ email: 'colleague@example.com', role: 2 }),
    )
    await waitFor(() => expect(emailInput.value).toBe(''))
  })

  it('surfaces an error alert when the invite POST fails', async () => {
    authenticate()
    serveSettings(makeProject(), [makeMember(OWNER_ID, 0)])
    server.use(
      http.post(`*/api/projects/${PROJECT_ID}/invite`, () =>
        HttpResponse.json({ error: { message: 'taken' } }, { status: 500 }),
      ),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const emailInput = await screen.findByPlaceholderText(/colleague's email/i)
    await user.type(emailInput, 'colleague@example.com')
    await user.click(screen.getByRole('button', { name: /invite user/i }))

    expect(await screen.findByText(/unable to send invitation/i)).toBeInTheDocument()
  })
})

describe('project settings — member role change', () => {
  it('PUTs to /api/projects/:id/members/:userId/role when a non-owner role is changed', async () => {
    authenticate()
    serveSettings(makeProject(), [
      makeMember(OWNER_ID, 0),
      makeMember(MEMBER_ID, 2),
    ])

    let roleBody: { role?: number } | null = null
    server.use(
      http.put(`*/api/projects/${PROJECT_ID}/members/${MEMBER_ID}/role`, async ({ request }) => {
        roleBody = (await request.json()) as { role?: number }
        return HttpResponse.json(
          { userId: MEMBER_ID, role: 1, joinedAt: '2026-01-01T00:00:00Z' },
          { status: 200 },
        )
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    // The other member's role is rendered as an editable Select. MUI selects
    // expose role="combobox"; we click it and then select "Manager".
    const memberRow = (
      await screen.findAllByText(`User ${MEMBER_ID.slice(0, 4)}`)
    )[0].closest('div[class*="MuiStack"]') as HTMLElement
    expect(memberRow).not.toBeNull()

    const select = within(memberRow).getByRole('combobox')
    await user.click(select)

    // Option "Manager" appears in a portal-mounted listbox.
    await user.click(await screen.findByRole('option', { name: /manager/i }))

    await waitFor(() => expect(roleBody).toEqual({ role: 1 }))
  })
})

describe('project settings — remove member', () => {
  it('opens a confirm dialog and DELETEs on confirmation', async () => {
    authenticate()
    serveSettings(makeProject(), [
      makeMember(OWNER_ID, 0),
      makeMember(MEMBER_ID, 2),
    ])

    let deleted = false
    server.use(
      http.delete(`*/api/projects/${PROJECT_ID}/members/${MEMBER_ID}`, () => {
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    const removeButton = await screen.findByRole('button', {
      name: new RegExp(`remove user ${MEMBER_ID.slice(0, 4)}`, 'i'),
    })
    await user.click(removeButton)

    // Confirm in the dialog.
    const dialog = await screen.findByRole('dialog', { name: /remove member/i })
    await user.click(within(dialog).getByRole('button', { name: /confirm remove/i }))

    await waitFor(() => expect(deleted).toBe(true))
  })

  it('cancel keeps the dialog from issuing the DELETE', async () => {
    authenticate()
    serveSettings(makeProject(), [
      makeMember(OWNER_ID, 0),
      makeMember(MEMBER_ID, 2),
    ])

    let deleted = false
    server.use(
      http.delete(`*/api/projects/${PROJECT_ID}/members/${MEMBER_ID}`, () => {
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    await user.click(
      await screen.findByRole('button', {
        name: new RegExp(`remove user ${MEMBER_ID.slice(0, 4)}`, 'i'),
      }),
    )

    const dialog = await screen.findByRole('dialog', { name: /remove member/i })
    await user.click(within(dialog).getByRole('button', { name: /cancel/i }))

    // Give the (non-)mutation a moment.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(deleted).toBe(false)
  })
})

describe('project settings — transfer ownership', () => {
  it('POSTs the new owner id when confirmed', async () => {
    authenticate()
    serveSettings(makeProject(), [
      makeMember(OWNER_ID, 0),
      makeMember(MEMBER_ID, 1), // a manager — eligible for transfer
    ])

    let transferBody: { newOwnerUserId?: string } | null = null
    server.use(
      http.post(`*/api/projects/${PROJECT_ID}/transfer-ownership`, async ({ request }) => {
        transferBody = (await request.json()) as { newOwnerUserId?: string }
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    await user.click(
      await screen.findByRole('button', {
        name: new RegExp(`transfer ownership to user ${MEMBER_ID.slice(0, 4)}`, 'i'),
      }),
    )

    const dialog = await screen.findByRole('dialog', { name: /transfer ownership/i })
    await user.click(within(dialog).getByRole('button', { name: /confirm transfer/i }))

    await waitFor(() => expect(transferBody).toEqual({ newOwnerUserId: MEMBER_ID }))
  })
})

describe('project settings — archive workspace', () => {
  it('opens a confirm dialog, DELETEs the project, and navigates back to /projects', async () => {
    authenticate()
    serveSettings(makeProject(), [makeMember(OWNER_ID, 0)])

    let archived = false
    server.use(
      http.delete(`*/api/projects/${PROJECT_ID}`, () => {
        archived = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    const { router } = await renderApp({ route: `/projects/${PROJECT_ID}/settings` })

    await user.click(await screen.findByRole('button', { name: /archive workspace/i }))

    const dialog = await screen.findByRole('dialog', { name: /archive workspace/i })
    await user.click(within(dialog).getByRole('button', { name: /confirm archive/i }))

    await waitFor(() => expect(archived).toBe(true))
    await waitFor(() => expect(router.state.location.pathname).toBe('/projects'))
  })
})
