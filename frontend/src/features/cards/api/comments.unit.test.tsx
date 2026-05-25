import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  useComments,
  useCreateComment,
  useDeleteComment,
  useUpdateComment,
} from './comments'
import { server } from '@/testing/msw/server'

function wrap() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

const COMMENT = {
  id: 'c-1',
  cardId: 'card-1',
  authorId: 'u-1',
  content: 'first comment',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('useComments', () => {
  it('fetches comments for a card and exposes them via the query state', async () => {
    server.use(
      http.get('*/api/cards/card-1/comments', () => HttpResponse.json([COMMENT], { status: 200 })),
    )

    const { result } = renderHook(() => useComments('card-1'), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual([COMMENT])
  })

  it('does not fire the request when cardId is undefined', () => {
    const { result } = renderHook(() => useComments(undefined), { wrapper: wrap() })

    expect(result.current.fetchStatus).toBe('idle')
  })
})

describe('useCreateComment', () => {
  it('POSTs the new comment body to /api/cards/:id/comments', async () => {
    let receivedBody: { content?: string } | null = null
    server.use(
      http.post('*/api/cards/card-1/comments', async ({ request }) => {
        receivedBody = (await request.json()) as { content?: string }
        return HttpResponse.json(COMMENT, { status: 201 })
      }),
    )

    const { result } = renderHook(() => useCreateComment(), { wrapper: wrap() })

    await result.current.mutateAsync({ cardId: 'card-1', content: 'hello' })
    expect(receivedBody).toEqual({ content: 'hello' })
  })
})

describe('useUpdateComment', () => {
  it('PUTs to /api/comments/:id with the new content', async () => {
    let receivedBody: { content?: string } | null = null
    server.use(
      http.put('*/api/comments/c-1', async ({ request }) => {
        receivedBody = (await request.json()) as { content?: string }
        return HttpResponse.json({ ...COMMENT, content: 'edited' }, { status: 200 })
      }),
    )

    const { result } = renderHook(() => useUpdateComment(), { wrapper: wrap() })

    await result.current.mutateAsync({ id: 'c-1', cardId: 'card-1', content: 'edited' })
    expect(receivedBody).toEqual({ content: 'edited' })
  })
})

describe('useDeleteComment', () => {
  it('DELETEs /api/comments/:id', async () => {
    let deleted = false
    server.use(
      http.delete('*/api/comments/c-1', () => {
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useDeleteComment(), { wrapper: wrap() })

    await result.current.mutateAsync({ id: 'c-1', cardId: 'card-1' })
    expect(deleted).toBe(true)
  })
})
