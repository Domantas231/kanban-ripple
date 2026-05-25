import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { render, screen, cleanup, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { appTheme } from '@/app/theme'
import { useAuthStore } from '@/features/auth'
import { server } from '@/testing/msw/server'
import { SubscribeButton } from './SubscribeButton'

const USER_ID = '11111111-1111-1111-1111-111111111111'
const CARD_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc'
const BOARD_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'

beforeEach(() => {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'tok')
})

afterEach(() => {
  cleanup()
})

function renderButton(props: React.ComponentProps<typeof SubscribeButton>) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <ThemeProvider theme={appTheme}>
      <QueryClientProvider client={queryClient}>
        <SubscribeButton {...props} />
      </QueryClientProvider>
    </ThemeProvider>,
  )
}

describe('SubscribeButton — unsubscribed state', () => {
  it('shows the "Subscribe" affordance when the user is not in the subscriber list', async () => {
    server.use(
      http.get(`*/api/cards/${CARD_ID}/subscriptions`, () => HttpResponse.json([], { status: 200 })),
    )

    renderButton({ entityType: 0, entityId: CARD_ID })

    expect(await screen.findByRole('button', { name: /subscribe/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /unsubscribe/i })).not.toBeInTheDocument()
  })

  it('subscribes when clicked while unsubscribed', async () => {
    let subscribed = false
    let postCalled = false

    server.use(
      http.get(`*/api/cards/${CARD_ID}/subscriptions`, () =>
        HttpResponse.json(subscribed ? [USER_ID] : [], { status: 200 }),
      ),
      http.post('*/api/subscriptions', () => {
        postCalled = true
        subscribed = true
        return HttpResponse.json(
          {
            id: 'sub-1',
            userId: USER_ID,
            entityType: 0,
            entityId: CARD_ID,
            createdAt: '2026-01-01T00:00:00Z',
          },
          { status: 200 },
        )
      }),
    )

    const user = userEvent.setup()
    renderButton({ entityType: 0, entityId: CARD_ID })

    const button = await screen.findByRole('button', { name: /subscribe/i })
    await user.click(button)

    await waitFor(() => expect(postCalled).toBe(true))
    expect(await screen.findByRole('button', { name: /unsubscribe/i })).toBeInTheDocument()
  })
})

describe('SubscribeButton — subscribed state', () => {
  it('shows the "Unsubscribe" affordance when the current user is already subscribed', async () => {
    server.use(
      http.get(`*/api/cards/${CARD_ID}/subscriptions`, () => HttpResponse.json([USER_ID], { status: 200 })),
    )

    renderButton({ entityType: 0, entityId: CARD_ID })

    expect(await screen.findByRole('button', { name: /unsubscribe/i })).toBeInTheDocument()
  })

  it('issues DELETE /api/subscriptions with entity params when toggled off', async () => {
    let subscribed = true
    let deleteUrl: string | null = null

    server.use(
      http.get(`*/api/cards/${CARD_ID}/subscriptions`, () =>
        HttpResponse.json(subscribed ? [USER_ID] : [], { status: 200 }),
      ),
      http.delete('*/api/subscriptions', ({ request }) => {
        deleteUrl = request.url
        subscribed = false
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    renderButton({ entityType: 0, entityId: CARD_ID })

    const button = await screen.findByRole('button', { name: /unsubscribe/i })
    await user.click(button)

    await waitFor(() => expect(deleteUrl).not.toBeNull())
    expect(deleteUrl).toContain('entityType=Card')
    expect(deleteUrl).toContain(`entityId=${CARD_ID}`)
  })
})

describe('SubscribeButton — entity types', () => {
  it('queries the board endpoint when entityType=3 (Board)', async () => {
    let boardCalled = false
    server.use(
      http.get(`*/api/boards/${BOARD_ID}/subscriptions`, () => {
        boardCalled = true
        return HttpResponse.json([], { status: 200 })
      }),
    )

    renderButton({ entityType: 3, entityId: BOARD_ID })

    await waitFor(() => expect(boardCalled).toBe(true))
  })
})

describe('SubscribeButton — disabled', () => {
  it('renders as disabled when the disabled prop is set', async () => {
    server.use(
      http.get(`*/api/cards/${CARD_ID}/subscriptions`, () => HttpResponse.json([], { status: 200 })),
    )

    renderButton({ entityType: 0, entityId: CARD_ID, disabled: true })

    const button = await screen.findByRole('button', { name: /subscribe/i })
    expect(button).toBeDisabled()
  })
})
