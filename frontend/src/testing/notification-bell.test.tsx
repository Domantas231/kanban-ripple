import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth'
import type { Notification } from '@/lib/types'
import { renderApp } from './renderApp'
import { server } from './msw/server'

// The Sidebar mounts a Notifications nav item whose icon is a MUI <Badge>.
// The unread count is derived client-side by paginating /api/notifications and
// counting items where isRead=false (see getUnreadCount in
// features/notifications/api/notifications.ts), so we mock that endpoint.
//
// We assert against the rendered MUI Badge classes / textContent because the
// threshold logic (dot vs number vs invisible) is the only part worth
// regression-testing, and there's no semantic role for the badge itself.

function authenticate() {
  useAuthStore
    .getState()
    .setAuth(
      { id: '11111111-1111-1111-1111-111111111111', email: 'user@example.com' },
      'access-token',
    )
}

function makeNotification(id: number, isRead: boolean): Notification {
  return {
    id: `notif-${id}`,
    userId: '11111111-1111-1111-1111-111111111111',
    type: 0 as never,
    title: `Notification ${id}`,
    message: '',
    isRead,
    createdAt: '2026-05-01T00:00:00Z',
  }
}

function mockNotificationsWithUnread(unread: number) {
  // Single page: the unread-count derivation paginates with pageSize=100.
  // As long as unread<=100 the count completes in one round trip.
  const items = Array.from({ length: unread }, (_, i) => makeNotification(i, false))
  server.use(
    http.get('*/api/notifications', () =>
      HttpResponse.json(
        { items, page: 1, pageSize: 100, totalCount: items.length },
        { status: 200 },
      ),
    ),
  )
}

async function renderProjectsPage() {
  authenticate()
  return renderApp({ route: '/projects' })
}

// In jsdom the desktop sidebar may resolve to display:none via MUI breakpoints,
// but keepMounted on the mobile drawer ensures the button exists in the DOM.
// We pass `hidden: true` so RTL doesn't filter visually-hidden elements out.
async function findNotificationButtonsWithBadge(): Promise<HTMLElement[]> {
  const buttons = await screen.findAllByRole('button', { name: /notifications/i, hidden: true })
  return buttons.filter((button) => button.querySelector('.MuiBadge-badge') !== null)
}

function getBadge(button: HTMLElement): Element {
  const badge = button.querySelector('.MuiBadge-badge')
  if (!badge) {
    throw new Error('Notifications button has no MuiBadge-badge child')
  }
  return badge
}

describe('notifications nav — badge thresholds', () => {
  it('keeps the badge invisible when nothing is unread', async () => {
    mockNotificationsWithUnread(0)
    await renderProjectsPage()

    const buttons = await findNotificationButtonsWithBadge()
    expect(buttons.length).toBeGreaterThan(0)
    // The query fires asynchronously; the initial render shows the default
    // (count=undefined → 0 → invisible). Allow the queryFn to settle.
    for (const button of buttons) {
      expect(getBadge(button).classList.contains('MuiBadge-invisible')).toBe(true)
    }
  })

  it('shows a dot (no number) when 1–3 are unread', async () => {
    mockNotificationsWithUnread(2)
    await renderProjectsPage()

    // The sidebar starts with count=0 (invisible) and updates once the
    // query resolves. Wait for at least one badge to enter the dot state.
    await screen.findAllByText(
      (_, element) =>
        element?.classList.contains('MuiBadge-badge') === true &&
        element.classList.contains('MuiBadge-dot') &&
        !element.classList.contains('MuiBadge-invisible'),
    )

    const buttons = await findNotificationButtonsWithBadge()
    for (const button of buttons) {
      const badge = getBadge(button)
      expect(badge.classList.contains('MuiBadge-dot')).toBe(true)
      expect(badge.classList.contains('MuiBadge-invisible')).toBe(false)
      expect(badge.textContent).toBe('')
    }
  })

  it('shows the unread count when more than 3 are unread', async () => {
    mockNotificationsWithUnread(7)
    await renderProjectsPage()

    await screen.findAllByText('7', { selector: '.MuiBadge-badge' })

    const buttons = await findNotificationButtonsWithBadge()
    for (const button of buttons) {
      const badge = getBadge(button)
      expect(badge.classList.contains('MuiBadge-dot')).toBe(false)
      expect(badge.textContent).toBe('7')
    }
  })

  it('caps the displayed count at 99+', async () => {
    mockNotificationsWithUnread(100)
    await renderProjectsPage()

    // MUI Badge with max=99 renders "99+" once 100 unreads load.
    await screen.findAllByText('99+', { selector: '.MuiBadge-badge' })

    const buttons = await findNotificationButtonsWithBadge()
    for (const button of buttons) {
      expect(getBadge(button).textContent).toBe('99+')
    }
  })
})

describe('notifications nav — drawer', () => {
  it('opens the notifications drawer with tabs when the nav item is clicked', async () => {
    mockNotificationsWithUnread(1)
    const user = userEvent.setup()
    await renderProjectsPage()

    const [button] = await findNotificationButtonsWithBadge()
    await user.click(button)

    // The drawer hosts a tablist with the "Unread" / "All" tabs.
    const tabs = await screen.findAllByRole('tab', { hidden: true })
    expect(tabs.length).toBeGreaterThan(0)
    const tabLabels = tabs.map((t) => t.textContent ?? '')
    expect(tabLabels.some((label) => /unread/i.test(label))).toBe(true)
    expect(tabLabels.some((label) => /\ball\b/i.test(label))).toBe(true)
  })

  it('updates the badge when the unread query refetches with a new value', async () => {
    let unread = 1
    server.use(
      http.get('*/api/notifications', () => {
        const items = Array.from({ length: unread }, (_, i) => makeNotification(i, false))
        return HttpResponse.json(
          { items, page: 1, pageSize: 100, totalCount: items.length },
          { status: 200 },
        )
      }),
    )

    const { queryClient } = await renderProjectsPage()

    // Wait for the initial dot variant.
    await screen.findAllByText(
      (_, element) =>
        element?.classList.contains('MuiBadge-badge') === true &&
        element.classList.contains('MuiBadge-dot') &&
        !element.classList.contains('MuiBadge-invisible'),
    )

    unread = 5
    await queryClient.invalidateQueries({ queryKey: ['notifications'] })

    const updatedBadges = await screen.findAllByText('5', { selector: '.MuiBadge-badge' })
    expect(updatedBadges[0].classList.contains('MuiBadge-dot')).toBe(false)

    const after = await findNotificationButtonsWithBadge()
    expect(after.some((b) => within(b).queryByText('5'))).toBe(true)
  })
})
