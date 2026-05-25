import { screen, waitForElementToBeRemoved, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import { renderApp } from './renderApp'
import { server } from './msw/server'

function authenticate() {
  useAuthStore
    .getState()
    .setAuth(
      { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
      'access-token',
    )
}

async function openDialogFromEmptyState() {
  authenticate()
  const user = userEvent.setup()
  await renderApp({ route: '/projects' })

  // From the empty state, the EmptyState exposes a "Create Workspace" button.
  await user.click(await screen.findByRole('button', { name: /create workspace/i }))

  // Wait for dialog title.
  const dialog = await screen.findByRole('dialog')
  return { user, dialog }
}

describe('create project dialog', () => {
  it('shows a zod error when submitted with an empty name', async () => {
    let createCalled = false
    server.use(
      http.post('*/api/projects', () => {
        createCalled = true
        return HttpResponse.json({}, { status: 200 })
      }),
    )

    const { user, dialog } = await openDialogFromEmptyState()

    await user.click(within(dialog).getByRole('button', { name: /^create$/i }))

    expect(await within(dialog).findByText(/workspace name is required/i)).toBeInTheDocument()
    expect(createCalled).toBe(false)
  })

  it('rejects whitespace-only names (zod trim+min(1))', async () => {
    let createCalled = false
    server.use(
      http.post('*/api/projects', () => {
        createCalled = true
        return HttpResponse.json({}, { status: 200 })
      }),
    )

    const { user, dialog } = await openDialogFromEmptyState()

    await user.type(within(dialog).getByLabelText(/workspace name/i), '   ')
    await user.click(within(dialog).getByRole('button', { name: /^create$/i }))

    expect(await within(dialog).findByText(/workspace name is required/i)).toBeInTheDocument()
    expect(createCalled).toBe(false)
  })

  it('shows an error alert when the API returns a 500', async () => {
    server.use(
      http.post('*/api/projects', () =>
        HttpResponse.json({ error: { message: 'oops' } }, { status: 500 }),
      ),
    )

    const { user, dialog } = await openDialogFromEmptyState()

    await user.type(within(dialog).getByLabelText(/workspace name/i), 'My Workspace')
    await user.click(within(dialog).getByRole('button', { name: /^create$/i }))

    expect(await within(dialog).findByText(/unable to create workspace/i)).toBeInTheDocument()
  })

  it('closes the dialog and refreshes the project list on success', async () => {
    // Initial GET returns empty; after the POST, GET returns the new workspace.
    let createdName: string | null = null
    server.use(
      http.get('*/api/projects', () =>
        HttpResponse.json(
          createdName === null
            ? { items: [], page: 1, pageSize: 25, totalCount: 0 }
            : {
                items: [
                  {
                    id: 'p-1',
                    name: createdName,
                    ownerId: '11111111-1111-1111-1111-111111111111',
                    memberCount: 1,
                    boardCount: 0,
                    createdAt: '2026-01-01T00:00:00Z',
                    updatedAt: '2026-01-01T00:00:00Z',
                  },
                ],
                page: 1,
                pageSize: 25,
                totalCount: 1,
              },
          { status: 200 },
        ),
      ),
      http.post('*/api/projects', async ({ request }) => {
        const body = (await request.json()) as { name: string }
        createdName = body.name
        return HttpResponse.json(
          {
            id: 'p-1',
            name: body.name,
            ownerId: '11111111-1111-1111-1111-111111111111',
            memberCount: 1,
            boardCount: 0,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          },
          { status: 201 },
        )
      }),
    )

    const { user, dialog } = await openDialogFromEmptyState()

    await user.type(within(dialog).getByLabelText(/workspace name/i), 'Brand New')
    await user.click(within(dialog).getByRole('button', { name: /^create$/i }))

    // Dialog closes after the mutation resolves.
    await waitForElementToBeRemoved(() => screen.queryByRole('dialog'))

    // The list refetches and renders the new workspace card.
    expect(await screen.findByRole('heading', { level: 6, name: 'Brand New' })).toBeInTheDocument()
  })
})
