import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { boardsQueryKeys } from '@/features/boards'
import {
  useArchiveCard,
  useCard,
  useCards,
  useCreateCard,
  useMoveCard,
  useUpdateCard,
} from './cards'
import { server } from '@/testing/msw/server'
import type { Card, PaginatedResponse } from '@/lib/types'

function makeWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }

  return { queryClient, Wrapper }
}

const CARD: Card = {
  id: 'card-1',
  columnId: 'col-1',
  title: 'A task',
  position: 1000,
  version: 1,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('useCards / useCard', () => {
  it('useCards returns the paginated card list for a board', async () => {
    server.use(
      http.get('*/api/boards/board-1/cards', () =>
        HttpResponse.json(
          { items: [CARD], page: 1, pageSize: 50, totalCount: 1 },
          { status: 200 },
        ),
      ),
    )

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useCards('board-1'), { wrapper: Wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.items).toEqual([CARD])
  })

  it('useCard fetches a single card detail', async () => {
    server.use(http.get('*/api/cards/card-1', () => HttpResponse.json(CARD, { status: 200 })))

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useCard('card-1'), { wrapper: Wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(CARD)
  })
})

describe('useCreateCard', () => {
  it('POSTs to /api/columns/:columnId/cards and returns the created card', async () => {
    let receivedBody: { title?: string } | null = null
    server.use(
      http.post('*/api/columns/col-1/cards', async ({ request }) => {
        receivedBody = (await request.json()) as { title?: string }
        return HttpResponse.json({ ...CARD, title: 'New' }, { status: 201 })
      }),
    )

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useCreateCard(), { wrapper: Wrapper })

    const created = await result.current.mutateAsync({ columnId: 'col-1', data: { title: 'New' } })
    expect(receivedBody).toMatchObject({ title: 'New' })
    expect(created.title).toBe('New')
  })
})

describe('useUpdateCard', () => {
  it('PUTs to /api/cards/:id with the version and returns the updated card', async () => {
    let receivedBody: { version?: number; title?: string } | null = null
    server.use(
      http.put('*/api/cards/card-1', async ({ request }) => {
        receivedBody = (await request.json()) as { version?: number; title?: string }
        return HttpResponse.json({ ...CARD, title: 'Renamed', version: 2 }, { status: 200 })
      }),
    )

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useUpdateCard(), { wrapper: Wrapper })

    const updated = await result.current.mutateAsync({
      id: 'card-1',
      data: { title: 'Renamed', version: 1 },
    })

    expect(receivedBody).toMatchObject({ title: 'Renamed', version: 1 })
    expect(updated.version).toBe(2)
  })

  it('rejects with a 409 when the version is stale (optimistic concurrency)', async () => {
    server.use(
      http.put('*/api/cards/card-1', () =>
        HttpResponse.json({ error: { message: 'version' } }, { status: 409 }),
      ),
    )

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useUpdateCard(), { wrapper: Wrapper })

    await expect(
      result.current.mutateAsync({ id: 'card-1', data: { title: 'X', version: 0 } }),
    ).rejects.toMatchObject({ response: { status: 409 } })
  })
})

describe('useArchiveCard', () => {
  it('DELETEs /api/cards/:id', async () => {
    let archived = false
    server.use(
      http.delete('*/api/cards/card-1', () => {
        archived = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { Wrapper } = makeWrapper()
    const { result } = renderHook(() => useArchiveCard(), { wrapper: Wrapper })

    await result.current.mutateAsync('card-1')
    expect(archived).toBe(true)
  })
})

describe('useMoveCard — optimistic updates', () => {
  it('optimistically swaps the card columnId in the board cards cache before the API resolves', async () => {
    const { queryClient, Wrapper } = makeWrapper()
    const initialCard: Card = { ...CARD, columnId: 'col-1', position: 1000 }
    const cachedPage: PaginatedResponse<Card> = {
      items: [initialCard],
      page: 1,
      pageSize: 50,
      totalCount: 1,
    }
    queryClient.setQueryData(
      [...boardsQueryKeys.boardCards('board-1'), 1, 50],
      cachedPage,
    )

    let didResolve: () => void = () => {}
    const apiCallSettled = new Promise<void>((resolve) => {
      didResolve = resolve
    })

    server.use(
      http.put('*/api/cards/card-1/move', async () => {
        // Block the response until the test allows it through, so we can
        // observe the optimistic state.
        await apiCallSettled
        return HttpResponse.json({ ...initialCard, columnId: 'col-2', position: 1500 }, { status: 200 })
      }),
    )

    const { result } = renderHook(() => useMoveCard(), { wrapper: Wrapper })

    const promise = result.current.mutateAsync({
      id: 'card-1',
      boardId: 'board-1',
      data: { columnId: 'col-2', position: 1500 },
    })

    // onMutate is async (cancelQueries → setQueriesData). Wait for the
    // optimistic state to land.
    await waitFor(() => {
      const cache = queryClient.getQueryData<PaginatedResponse<Card>>(
        [...boardsQueryKeys.boardCards('board-1'), 1, 50],
      )
      expect(cache?.items[0]).toMatchObject({ columnId: 'col-2', position: 1500 })
    })

    didResolve()
    await promise
  })

  it('rolls back to the previous cache when the move fails', async () => {
    const { queryClient, Wrapper } = makeWrapper()
    const initialCard: Card = { ...CARD, columnId: 'col-1', position: 1000 }
    const cachedPage: PaginatedResponse<Card> = {
      items: [initialCard],
      page: 1,
      pageSize: 50,
      totalCount: 1,
    }
    queryClient.setQueryData(
      [...boardsQueryKeys.boardCards('board-1'), 1, 50],
      cachedPage,
    )

    server.use(
      http.put('*/api/cards/card-1/move', () =>
        HttpResponse.json({ error: { message: 'denied' } }, { status: 403 }),
      ),
    )

    const { result } = renderHook(() => useMoveCard(), { wrapper: Wrapper })

    await expect(
      result.current.mutateAsync({
        id: 'card-1',
        boardId: 'board-1',
        data: { columnId: 'col-2', position: 1500 },
      }),
    ).rejects.toBeDefined()

    // After rollback, the cache should match the original snapshot.
    const after = queryClient.getQueryData<PaginatedResponse<Card>>(
      [...boardsQueryKeys.boardCards('board-1'), 1, 50],
    )
    expect(after?.items[0]).toMatchObject({ columnId: 'col-1', position: 1000 })
  })
})
