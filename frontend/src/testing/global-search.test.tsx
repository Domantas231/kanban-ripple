import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'tok')
}

async function openSearchDialog(user: ReturnType<typeof userEvent.setup>) {
  // The topbar's GlobalSearchBar trigger is a `<button>` containing the
  // literal label "Search..." (with three dots). The projects route also
  // renders a TextField with placeholder "Search workspaces..." — disambiguate
  // by the trailing ellipsis label, which only the global trigger has.
  const trigger = await screen.findByText('Search...')
  await user.click(trigger)
}

describe('global search — open and close', () => {
  it('opens a search dialog when the topbar search button is clicked', async () => {
    authenticate()
    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openSearchDialog(user)

    expect(
      await screen.findByPlaceholderText(/search workspaces, boards, tasks/i),
    ).toBeInTheDocument()
  })

  it('closes the dialog when Escape is pressed', async () => {
    authenticate()
    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openSearchDialog(user)
    await screen.findByPlaceholderText(/search workspaces, boards, tasks/i)

    await user.keyboard('{Escape}')

    await waitFor(() =>
      expect(screen.queryByPlaceholderText(/search workspaces, boards, tasks/i)).not.toBeInTheDocument(),
    )
  })
})

describe('global search — querying', () => {
  it('debounces and shows results from /api/search', async () => {
    authenticate()

    let lastQuery: string | null = null
    server.use(
      http.get('*/api/search', ({ request }) => {
        const url = new URL(request.url)
        lastQuery = url.searchParams.get('q')
        return HttpResponse.json(
          {
            items: [
              {
                id: 'card-1',
                type: 'card',
                name: 'Found Task',
                description: 'A task that matches',
                location: {
                  projectId: 'p-1',
                  projectName: 'Workspace',
                  boardId: 'b-1',
                  boardName: 'Board A',
                },
              },
            ],
          },
          { status: 200 },
        )
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openSearchDialog(user)

    const input = await screen.findByPlaceholderText(/search workspaces, boards, tasks/i)
    await user.type(input, 'found')

    expect(await screen.findByText('Found Task')).toBeInTheDocument()
    expect(lastQuery).toBe('found')
  })

  it('shows a no-results state when the API returns an empty list', async () => {
    authenticate()
    server.use(
      http.get('*/api/search', () =>
        HttpResponse.json({ items: [] }, { status: 200 }),
      ),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openSearchDialog(user)
    const input = await screen.findByPlaceholderText(/search workspaces, boards, tasks/i)
    await user.type(input, 'nothing matches')

    // The component renders a no-results message; we accept any of the typical
    // copy variants.
    await screen.findByText((text) => /no.*(results|matches)/i.test(text))
  })
})
