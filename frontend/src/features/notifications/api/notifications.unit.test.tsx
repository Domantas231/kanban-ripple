import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  getUnreadCount,
  useDeleteNotification,
  useMarkAllAsRead,
  useMarkAsRead,
  useNotifications,
} from './notifications'
import { server } from '@/testing/msw/server'
import type { Notification } from '@/lib/types'

function wrap() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

function makeNotification(id: number, isRead: boolean): Notification {
  return {
    id: `n-${id}`,
    userId: 'u-1',
    type: 0 as never,
    title: `Notif ${id}`,
    message: '',
    isRead,
    createdAt: '2026-01-01T00:00:00Z',
  }
}

describe('useNotifications', () => {
  it('fetches a paginated notifications page', async () => {
    server.use(
      http.get('*/api/notifications', () =>
        HttpResponse.json(
          {
            items: [makeNotification(1, false), makeNotification(2, true)],
            page: 1,
            pageSize: 20,
            totalCount: 2,
          },
          { status: 200 },
        ),
      ),
    )

    const { result } = renderHook(() => useNotifications(1, 20), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.items).toHaveLength(2)
  })
})

describe('getUnreadCount', () => {
  it('paginates through results and counts only unread items', async () => {
    let calls = 0
    // We force pagination by reporting a smaller pageSize than the actual
    // data; the request supplies pageSize=100 but the response can claim
    // pageSize=4 to force a second round-trip given totalCount=5.
    server.use(
      http.get('*/api/notifications', ({ request }) => {
        const url = new URL(request.url)
        const page = Number(url.searchParams.get('page'))
        calls += 1

        if (page === 1) {
          // 3 unread (1, 2, 4), 1 read (3) — Math.ceil(5/4) = 2, so page 2 is fetched.
          return HttpResponse.json(
            {
              items: [
                makeNotification(1, false),
                makeNotification(2, false),
                makeNotification(3, true),
                makeNotification(4, false),
              ],
              page: 1,
              pageSize: 4,
              totalCount: 5,
            },
            { status: 200 },
          )
        }

        return HttpResponse.json(
          {
            items: [makeNotification(5, false)],
            page: 2,
            pageSize: 4,
            totalCount: 5,
          },
          { status: 200 },
        )
      }),
    )

    const count = await getUnreadCount()
    expect(count).toBe(4)
    expect(calls).toBe(2)
  })
})

describe('useMarkAsRead', () => {
  it('PUTs to /api/notifications/:id/read', async () => {
    let putUrl: string | null = null
    server.use(
      http.put('*/api/notifications/:id/read', ({ request }) => {
        putUrl = request.url
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useMarkAsRead(), { wrapper: wrap() })
    await result.current.mutateAsync('notif-99')
    expect(putUrl).toContain('/api/notifications/notif-99/read')
  })
})

describe('useMarkAllAsRead', () => {
  it('PUTs to /api/notifications/read-all', async () => {
    let hit = false
    server.use(
      http.put('*/api/notifications/read-all', () => {
        hit = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useMarkAllAsRead(), { wrapper: wrap() })
    await result.current.mutateAsync()
    expect(hit).toBe(true)
  })
})

describe('useDeleteNotification', () => {
  it('DELETEs /api/notifications/:id', async () => {
    let deleteUrl: string | null = null
    server.use(
      http.delete('*/api/notifications/:id', ({ request }) => {
        deleteUrl = request.url
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useDeleteNotification(), { wrapper: wrap() })
    await result.current.mutateAsync('notif-77')
    expect(deleteUrl).toContain('/api/notifications/notif-77')
  })
})
