import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  useArchiveBoard,
  useBoard,
  useBoards,
  useCreateBoard,
  useUpdateBoard,
} from './boards'
import { server } from '@/testing/msw/server'
import type { Board } from '@/lib/types'

function wrap() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

const BOARD: Board = {
  id: 'b-1',
  projectId: 'p-1',
  name: 'Sprint 1',
  position: 0,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('useBoards', () => {
  it('fetches the project boards list', async () => {
    server.use(
      http.get('*/api/projects/p-1/boards', () => HttpResponse.json([BOARD], { status: 200 })),
    )

    const { result } = renderHook(() => useBoards('p-1'), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual([BOARD])
  })

  it('does not fetch when projectId is undefined', () => {
    const { result } = renderHook(() => useBoards(undefined), { wrapper: wrap() })
    expect(result.current.fetchStatus).toBe('idle')
  })
})

describe('useBoard', () => {
  it('fetches a single board by id', async () => {
    server.use(
      http.get('*/api/boards/b-1', () => HttpResponse.json(BOARD, { status: 200 })),
    )

    const { result } = renderHook(() => useBoard('b-1'), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(BOARD)
  })
})

describe('useCreateBoard', () => {
  it('POSTs the new board to /api/projects/:id/boards and returns the created entity', async () => {
    let receivedBody: { name?: string } | null = null
    server.use(
      http.post('*/api/projects/p-1/boards', async ({ request }) => {
        receivedBody = (await request.json()) as { name?: string }
        return HttpResponse.json(BOARD, { status: 201 })
      }),
    )

    const { result } = renderHook(() => useCreateBoard(), { wrapper: wrap() })

    const created = await result.current.mutateAsync({ projectId: 'p-1', data: { name: 'Sprint 1' } })

    expect(receivedBody).toEqual({ name: 'Sprint 1' })
    expect(created).toEqual(BOARD)
  })

  it('rejects when the API returns a non-2xx status', async () => {
    server.use(
      http.post('*/api/projects/p-1/boards', () =>
        HttpResponse.json({ error: { message: 'oops' } }, { status: 500 }),
      ),
    )

    const { result } = renderHook(() => useCreateBoard(), { wrapper: wrap() })

    await expect(
      result.current.mutateAsync({ projectId: 'p-1', data: { name: 'x' } }),
    ).rejects.toBeDefined()
  })
})

describe('useUpdateBoard', () => {
  it('PUTs to /api/boards/:id and returns the updated entity', async () => {
    server.use(
      http.put('*/api/boards/b-1', () => HttpResponse.json({ ...BOARD, name: 'Renamed' }, { status: 200 })),
    )

    const { result } = renderHook(() => useUpdateBoard(), { wrapper: wrap() })

    const updated = await result.current.mutateAsync({
      id: 'b-1',
      data: { name: 'Renamed', position: 0 },
    })

    expect(updated.name).toBe('Renamed')
  })
})

describe('useArchiveBoard', () => {
  it('DELETEs /api/boards/:id', async () => {
    let archived = false
    server.use(
      http.delete('*/api/boards/b-1', () => {
        archived = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useArchiveBoard(), { wrapper: wrap() })
    await result.current.mutateAsync('b-1')
    expect(archived).toBe(true)
  })
})
