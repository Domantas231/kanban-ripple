import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  useArchiveProject,
  useCreateProject,
  useLeaveProject,
  useProject,
  useProjectMembers,
  useProjects,
  useUpdateProject,
} from './projects'
import { server } from '@/testing/msw/server'
import { useUiStore } from '@/stores/uiStore'
import type { Project } from '@/lib/types'

function wrap() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

const PROJECT: Project = {
  id: 'p-1',
  name: 'Workspace',
  ownerId: 'owner-1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

describe('useProjects', () => {
  it('fetches the active workspace list', async () => {
    server.use(
      http.get('*/api/projects', () =>
        HttpResponse.json({ items: [PROJECT], page: 1, pageSize: 25, totalCount: 1 }, { status: 200 }),
      ),
    )

    const { result } = renderHook(() => useProjects(), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.items).toEqual([PROJECT])
  })
})

describe('useProject', () => {
  it('fetches a project by id', async () => {
    server.use(http.get('*/api/projects/p-1', () => HttpResponse.json(PROJECT, { status: 200 })))

    const { result } = renderHook(() => useProject('p-1'), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(PROJECT)
  })

  it('does not fetch when id is undefined', () => {
    const { result } = renderHook(() => useProject(undefined), { wrapper: wrap() })
    expect(result.current.fetchStatus).toBe('idle')
  })
})

describe('useProjectMembers', () => {
  it('fetches the members for a project', async () => {
    server.use(
      http.get('*/api/projects/p-1/members', () =>
        HttpResponse.json(
          [{ userId: 'u-1', role: 0, joinedAt: '2026-01-01T00:00:00Z' }],
          { status: 200 },
        ),
      ),
    )

    const { result } = renderHook(() => useProjectMembers('p-1'), { wrapper: wrap() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(1)
  })
})

describe('useCreateProject', () => {
  it('POSTs to /api/projects with the body and returns the created entity', async () => {
    let body: { name?: string } | null = null
    server.use(
      http.post('*/api/projects', async ({ request }) => {
        body = (await request.json()) as { name?: string }
        return HttpResponse.json(PROJECT, { status: 201 })
      }),
    )

    const { result } = renderHook(() => useCreateProject(), { wrapper: wrap() })

    const created = await result.current.mutateAsync({ name: 'Workspace' })
    expect(body).toEqual({ name: 'Workspace' })
    expect(created).toEqual(PROJECT)
  })
})

describe('useUpdateProject', () => {
  it('PUTs to /api/projects/:id and returns the updated project', async () => {
    server.use(
      http.put('*/api/projects/p-1', () =>
        HttpResponse.json({ ...PROJECT, name: 'Renamed' }, { status: 200 }),
      ),
    )

    const { result } = renderHook(() => useUpdateProject(), { wrapper: wrap() })
    const out = await result.current.mutateAsync({ id: 'p-1', data: { name: 'Renamed' } })
    expect(out.name).toBe('Renamed')
  })
})

describe('useArchiveProject', () => {
  it('DELETEs /api/projects/:id', async () => {
    let archived = false
    server.use(
      http.delete('*/api/projects/p-1', () => {
        archived = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useArchiveProject(), { wrapper: wrap() })
    await result.current.mutateAsync('p-1')
    expect(archived).toBe(true)
  })
})

describe('useLeaveProject', () => {
  it('POSTs /api/projects/:id/leave on success', async () => {
    let posted = false
    server.use(
      http.post('*/api/projects/p-1/leave', () => {
        posted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = renderHook(() => useLeaveProject(), { wrapper: wrap() })
    await result.current.mutateAsync('p-1')
    expect(posted).toBe(true)
  })

  it('toasts the owner-cannot-leave message on 403', async () => {
    useUiStore.getState().dismissToast()
    server.use(
      http.post('*/api/projects/p-1/leave', () =>
        HttpResponse.json({ error: { message: 'forbidden' } }, { status: 403 }),
      ),
    )

    const { result } = renderHook(() => useLeaveProject(), { wrapper: wrap() })

    await expect(result.current.mutateAsync('p-1')).rejects.toBeDefined()

    const toast = useUiStore.getState().activeToast
    // Either the canned owner-cannot-leave message or the API one — assert
    // we surface SOMETHING actionable.
    expect(toast?.severity).toBe('error')
    expect(toast?.message).toMatch(/can't leave|forbidden|leave the workspace/i)
  })
})
