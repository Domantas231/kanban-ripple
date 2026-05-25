import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Notification } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

const USER_ID = '11111111-1111-1111-1111-111111111111'

function authenticate() {
  useAuthStore.getState().setAuth({ id: USER_ID, email: 'user@example.com' }, 'tok')
}

function makeNotification(id: number, isRead: boolean, overrides: Partial<Notification> = {}): Notification {
  return {
    id: `notif-${id}`,
    userId: USER_ID,
    type: 0 as never,
    title: `Notification ${id}`,
    message: `Body for notification ${id}`,
    isRead,
    createdAt: '2026-05-01T00:00:00Z',
    ...overrides,
  }
}

function serveNotifications(items: Notification[]) {
  server.use(
    http.get('*/api/notifications', () =>
      HttpResponse.json(
        { items, page: 1, pageSize: 100, totalCount: items.length },
        { status: 200 },
      ),
    ),
  )
}

async function openDrawer(user: ReturnType<typeof userEvent.setup>) {
  // The sidebar exposes the notifications drawer trigger as a NavListItem
  // labelled "Notifications" (role=button). On mobile and desktop the same
  // label appears once each (drawer + collapsed sidebar), so click the first.
  const buttons = await screen.findAllByRole('button', {
    name: /^notifications$/i,
    hidden: true,
  })
  await user.click(buttons[0])
}

describe('notifications drawer — empty states', () => {
  it('shows the "all caught up" copy when the All tab has no items', async () => {
    authenticate()
    serveNotifications([])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    // Switch to "All" tab — drawer defaults to "Unread".
    const tabs = await screen.findAllByRole('tab', { hidden: true })
    const allTab = tabs.find((t) => (t.textContent ?? '').trim() === 'All')
    expect(allTab).toBeDefined()
    await user.click(allTab as HTMLElement)

    expect(await screen.findByText(/all caught up/i)).toBeInTheDocument()
  })

  it('shows the "no unread" copy when the Unread tab has no items but All has read items', async () => {
    authenticate()
    serveNotifications([makeNotification(1, true)])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    expect(await screen.findByText(/no unread notifications/i)).toBeInTheDocument()
  })
})

describe('notifications drawer — interactions', () => {
  it('renders unread notifications and exposes a "Mark all read" button', async () => {
    authenticate()
    serveNotifications([makeNotification(1, false), makeNotification(2, false)])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    expect(await screen.findByText('Notification 1')).toBeInTheDocument()
    expect(screen.getByText('Notification 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /mark all as read/i })).toBeEnabled()
  })

  it('disables "Mark all read" when there is nothing unread', async () => {
    authenticate()
    serveNotifications([makeNotification(1, true)])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    // Switch to All so the read notification is visible (Unread is empty).
    const allTabs = await screen.findAllByRole('tab', { hidden: true })
    const allTab = allTabs.find((t) => (t.textContent ?? '').trim() === 'All')
    expect(allTab).toBeDefined()
    await user.click(allTab as HTMLElement)

    expect(await screen.findByText('Notification 1')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /mark all as read/i })).toBeDisabled()
  })

  it('marks a notification as read when its row is clicked, and POSTs to /read', async () => {
    authenticate()

    let putHit = false
    server.use(
      http.get('*/api/notifications', () =>
        HttpResponse.json(
          { items: [makeNotification(1, false)], page: 1, pageSize: 100, totalCount: 1 },
          { status: 200 },
        ),
      ),
      http.put('*/api/notifications/notif-1/read', () => {
        putHit = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    const item = await screen.findByText('Notification 1')
    await user.click(item)

    // The PUT is fire-and-forget; allow microtasks to flush.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(putHit).toBe(true)
  })

  it('renders the unread count in the Unread tab label when items exist', async () => {
    authenticate()
    serveNotifications([
      makeNotification(1, false),
      makeNotification(2, false),
      makeNotification(3, false),
    ])

    const user = userEvent.setup()
    await renderApp({ route: '/projects' })

    await openDrawer(user)

    const unreadTabs = await screen.findAllByRole('tab', { hidden: true })
    const unreadTab = unreadTabs.find((t) => /unread/i.test(t.textContent ?? ''))
    expect(unreadTab).toBeDefined()
    expect(within(unreadTab as HTMLElement).getByText(/Unread \(3\)/)).toBeInTheDocument()
  })
})
