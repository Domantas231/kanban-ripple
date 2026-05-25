import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { apiClient, configureApiClientAuth, configureApiClientNavigation } from './api-client'
import { useUiStore } from '@/stores/uiStore'
import { server } from '@/testing/msw/server'

// These tests exercise the global axios interceptors directly. Setup uses the
// shared MSW server (reset between tests), and we layer per-test handlers via
// server.use(...). The `apiClient` baseURL is empty by default, so absolute
// URLs (`http://localhost/...`) are passed through directly.

beforeEach(() => {
  // Reset auth bridge to its no-op default before each test.
  configureApiClientAuth({
    getAccessToken: () => null,
    applyRefreshedSession: () => {},
    clearSession: () => {},
  })
  // Reset navigation bridge to a no-op so tests that don't override it
  // never accidentally trigger window.location.assign in jsdom.
  configureApiClientNavigation({
    redirectToWorkspaces: () => {},
  })
})

afterEach(() => {
  // UI store leaks across tests if we don't dismiss toasts/dialogs.
  const ui = useUiStore.getState()
  ui.dismissToast()
  ui.closeConflictDialog()
  // Reset pathname so cross-test pollution doesn't break URL-aware handlers.
  if (window.location.pathname !== '/') {
    window.history.pushState({}, '', '/')
  }
})

describe('api-client request interceptor', () => {
  it('attaches the access token from the auth bridge as a Bearer header', async () => {
    let receivedAuthHeader: string | null = null

    server.use(
      http.get('http://localhost/api/projects', ({ request }) => {
        receivedAuthHeader = request.headers.get('authorization')
        return HttpResponse.json({ ok: true }, { status: 200 })
      }),
    )

    configureApiClientAuth({
      getAccessToken: () => 'token-123',
      applyRefreshedSession: () => {},
      clearSession: () => {},
    })

    await apiClient.get('http://localhost/api/projects')

    expect(receivedAuthHeader).toBe('Bearer token-123')
  })

  it('omits the Authorization header when no token is configured', async () => {
    let receivedAuthHeader: string | null = null

    server.use(
      http.get('http://localhost/api/projects', ({ request }) => {
        receivedAuthHeader = request.headers.get('authorization')
        return HttpResponse.json({ ok: true }, { status: 200 })
      }),
    )

    await apiClient.get('http://localhost/api/projects')

    expect(receivedAuthHeader).toBeNull()
  })
})

describe('api-client response interceptor — 401 silent refresh', () => {
  it('refreshes the session on 401 and retries the original request', async () => {
    let firstAttempt = true
    let retryAuthHeader: string | null = null

    server.use(
      http.get('http://localhost/api/projects', ({ request }) => {
        if (firstAttempt) {
          firstAttempt = false
          return HttpResponse.json({ error: { message: 'expired' } }, { status: 401 })
        }
        retryAuthHeader = request.headers.get('authorization')
        return HttpResponse.json({ retried: true }, { status: 200 })
      }),
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json(
          {
            userId: 'user-1',
            email: 'user@example.com',
            accessToken: 'new-token',
            accessTokenExpiresAt: '2030-01-01T00:00:00Z',
            refreshToken: 'new-refresh',
            refreshTokenExpiresAt: '2030-01-08T00:00:00Z',
          },
          { status: 200 },
        ),
      ),
    )

    let storedToken: string | null = 'old-token'
    const apply = vi.fn((_user, accessToken: string) => {
      storedToken = accessToken
    })

    configureApiClientAuth({
      getAccessToken: () => storedToken,
      applyRefreshedSession: apply,
      clearSession: vi.fn(),
    })

    const result = await apiClient.get('http://localhost/api/projects')

    expect(result.data).toEqual({ retried: true })
    expect(retryAuthHeader).toBe('Bearer new-token')
    expect(apply).toHaveBeenCalledWith(
      { id: 'user-1', email: 'user@example.com' },
      'new-token',
    )
  })

  it('clears the session and rejects when refresh itself fails', async () => {
    server.use(
      http.get('http://localhost/api/projects', () =>
        HttpResponse.json({ error: { message: 'unauth' } }, { status: 401 }),
      ),
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json({ error: { message: 'gone' } }, { status: 401 }),
      ),
    )

    const clearSession = vi.fn()
    configureApiClientAuth({
      getAccessToken: () => 'old-token',
      applyRefreshedSession: vi.fn(),
      clearSession,
    })

    // jsdom defaults to pathname '/', and the redirect helper checks
    // `pathname !== '/login'`. We can't easily mock `window.location.assign`
    // (jsdom marks it non-configurable), so just verify the session-clearing
    // side effect.
    await expect(apiClient.get('http://localhost/api/projects')).rejects.toMatchObject({
      response: { status: 401 },
    })
    expect(clearSession).toHaveBeenCalledOnce()
  })

  it('does not retry auth requests themselves (avoids infinite refresh loop)', async () => {
    let attempts = 0
    server.use(
      http.post('http://localhost/api/auth/login', () => {
        attempts += 1
        return HttpResponse.json({ error: { message: 'bad' } }, { status: 401 })
      }),
    )

    await expect(
      apiClient.post('http://localhost/api/auth/login', { email: 'a', password: 'b' }),
    ).rejects.toMatchObject({ response: { status: 401 } })
    expect(attempts).toBe(1)
  })

  it('does not retry the same request twice if the retried call also returns 401', async () => {
    let attempts = 0
    server.use(
      http.get('http://localhost/api/projects', () => {
        attempts += 1
        return HttpResponse.json({ error: { message: 'still bad' } }, { status: 401 })
      }),
      http.post('*/api/auth/refresh', () =>
        HttpResponse.json(
          {
            userId: 'user-1',
            email: 'user@example.com',
            accessToken: 'new-token',
            accessTokenExpiresAt: '2030-01-01T00:00:00Z',
            refreshToken: 'new-refresh',
            refreshTokenExpiresAt: '2030-01-08T00:00:00Z',
          },
          { status: 200 },
        ),
      ),
    )

    configureApiClientAuth({
      getAccessToken: () => 'token',
      applyRefreshedSession: vi.fn(),
      clearSession: vi.fn(),
    })

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toMatchObject({
      response: { status: 401 },
    })
    // First call (401) + one retry (also 401, but flagged _retry so it doesn't loop).
    expect(attempts).toBe(2)
  })
})

describe('api-client response interceptor — 403 toast', () => {
  it('shows a warning toast with the server message on 403', async () => {
    server.use(
      http.get('http://localhost/api/projects', () =>
        HttpResponse.json(
          { error: { message: 'You do not have access to that workspace.' } },
          { status: 403 },
        ),
      ),
    )

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toMatchObject({
      response: { status: 403 },
    })

    const toast = useUiStore.getState().activeToast
    expect(toast).not.toBeNull()
    expect(toast?.severity).toBe('warning')
    expect(toast?.message).toBe('You do not have access to that workspace.')
  })

  it('falls back to a default message when the 403 body has no error.message', async () => {
    server.use(http.get('http://localhost/api/projects', () => new HttpResponse(null, { status: 403 })))

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toBeDefined()

    expect(useUiStore.getState().activeToast?.message).toBe('Access Denied')
  })

  it('does not toast on auth-route 403s (caller handles it)', async () => {
    server.use(
      http.put('http://localhost/api/auth/password', () =>
        HttpResponse.json({ error: { message: 'Wrong password.' } }, { status: 403 }),
      ),
    )

    await expect(
      apiClient.put('http://localhost/api/auth/password', { currentPassword: 'x', newPassword: 'y' }),
    ).rejects.toBeDefined()

    expect(useUiStore.getState().activeToast).toBeNull()
  })
})

describe('api-client response interceptor — 403 project-access redirect', () => {
  const projectId = '11111111-1111-1111-1111-111111111111'

  it('redirects to /projects when a GET to /api/projects/{id} returns 403 while viewing that project', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.get(`http://localhost/api/projects/${projectId}`, () =>
        HttpResponse.json({ error: { message: 'Forbidden.' } }, { status: 403 }),
      ),
    )

    await expect(apiClient.get(`http://localhost/api/projects/${projectId}`)).rejects.toMatchObject({
      response: { status: 403 },
    })

    expect(redirect).toHaveBeenCalledOnce()
    // Toast still fires with the server message.
    expect(useUiStore.getState().activeToast?.message).toBe('Forbidden.')
  })

  it('redirects when a GET to a nested /api/projects/{id}/* resource returns 403 while on the project page', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.get(`http://localhost/api/projects/${projectId}/members`, () =>
        new HttpResponse(null, { status: 403 }),
      ),
    )

    await expect(
      apiClient.get(`http://localhost/api/projects/${projectId}/members`),
    ).rejects.toBeDefined()

    expect(redirect).toHaveBeenCalledOnce()
    // Falls back to access-lost copy when server omits a message.
    expect(useUiStore.getState().activeToast?.message).toBe(
      'You no longer have access to this workspace.',
    )
  })

  it('redirects when a GET to /api/boards/{id} returns 403 while on the project page', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    const boardId = '22222222-2222-2222-2222-222222222222'
    server.use(
      http.get(`http://localhost/api/boards/${boardId}`, () => new HttpResponse(null, { status: 403 })),
    )

    await expect(apiClient.get(`http://localhost/api/boards/${boardId}`)).rejects.toBeDefined()

    expect(redirect).toHaveBeenCalledOnce()
  })

  it('does NOT redirect on a 403 from a non-GET (POST/PUT/DELETE) — those are role denials, not access loss', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.post(`http://localhost/api/projects/${projectId}/invite`, () =>
        HttpResponse.json({ error: { message: 'Only the owner can invite managers.' } }, { status: 403 }),
      ),
    )

    await expect(
      apiClient.post(`http://localhost/api/projects/${projectId}/invite`, {
        email: 'x@y.com',
        role: 1,
      }),
    ).rejects.toBeDefined()

    expect(redirect).not.toHaveBeenCalled()
    // Toast still surfaces the role-denial message.
    expect(useUiStore.getState().activeToast?.message).toBe('Only the owner can invite managers.')
  })

  it('does NOT redirect on a 403 from a non-project endpoint (e.g. /api/notifications)', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.get('http://localhost/api/notifications', () => new HttpResponse(null, { status: 403 })),
    )

    await expect(apiClient.get('http://localhost/api/notifications')).rejects.toBeDefined()

    expect(redirect).not.toHaveBeenCalled()
  })

  it('does NOT redirect when the user is not on a /projects/{id} page (e.g. on /projects list)', async () => {
    window.history.pushState({}, '', '/projects')
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.get(`http://localhost/api/projects/${projectId}`, () =>
        new HttpResponse(null, { status: 403 }),
      ),
    )

    await expect(apiClient.get(`http://localhost/api/projects/${projectId}`)).rejects.toBeDefined()

    expect(redirect).not.toHaveBeenCalled()
  })

  it('does NOT redirect when the project URL has no GUID (e.g. /api/projects list)', async () => {
    window.history.pushState({}, '', `/projects/${projectId}`)
    const redirect = vi.fn()
    configureApiClientNavigation({ redirectToWorkspaces: redirect })

    server.use(
      http.get('http://localhost/api/projects', () => new HttpResponse(null, { status: 403 })),
    )

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toBeDefined()

    expect(redirect).not.toHaveBeenCalled()
  })
})

describe('api-client response interceptor — 409 conflict', () => {
  it('opens the conflict dialog on a generic 409', async () => {
    server.use(
      http.put('http://localhost/api/cards/abc', () =>
        HttpResponse.json({ error: { message: 'Version mismatch' } }, { status: 409 }),
      ),
    )

    await expect(apiClient.put('http://localhost/api/cards/abc', { title: 'x' })).rejects.toBeDefined()

    const ui = useUiStore.getState()
    expect(ui.conflictDialogOpen).toBe(true)
    expect(ui.conflictDialogMessage).toMatch(/Conflict/i)
    // No toast for a generic conflict — the dialog is the surface.
    expect(ui.activeToast).toBeNull()
  })

  it('shows a duplicate-name toast (not the conflict dialog) when error.code = DUPLICATE_NAME', async () => {
    server.use(
      http.post('http://localhost/api/projects', () =>
        HttpResponse.json(
          {
            error: {
              code: 'DUPLICATE_NAME',
              message: 'A workspace named "Alpha" already exists.',
            },
          },
          { status: 409 },
        ),
      ),
    )

    await expect(apiClient.post('http://localhost/api/projects', { name: 'Alpha' })).rejects.toBeDefined()

    const ui = useUiStore.getState()
    expect(ui.conflictDialogOpen).toBe(false)
    expect(ui.activeToast?.severity).toBe('error')
    expect(ui.activeToast?.message).toBe('A workspace named "Alpha" already exists.')
  })
})

describe('api-client response interceptor — 429 rate limiting', () => {
  it('shows a rate-limit toast with the Retry-After value when present', async () => {
    server.use(
      http.get(
        'http://localhost/api/projects',
        () =>
          new HttpResponse(JSON.stringify({ error: { message: 'too many' } }), {
            status: 429,
            headers: { 'retry-after': '12' },
          }),
      ),
    )

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toBeDefined()

    const toast = useUiStore.getState().activeToast
    expect(toast?.severity).toBe('warning')
    expect(toast?.message).toContain('Retry after 12 seconds.')
  })

  it('uses singular "second" for retry-after = 1', async () => {
    server.use(
      http.get(
        'http://localhost/api/projects',
        () => new HttpResponse(null, { status: 429, headers: { 'retry-after': '1' } }),
      ),
    )

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toBeDefined()
    expect(useUiStore.getState().activeToast?.message).toContain('Retry after 1 second.')
  })

  it('omits the retry-after suffix when the header is missing', async () => {
    server.use(
      http.get('http://localhost/api/projects', () => new HttpResponse(null, { status: 429 })),
    )

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toBeDefined()
    const toast = useUiStore.getState().activeToast
    expect(toast?.message).toBe('Rate limited, please wait.')
  })
})

describe('api-client response interceptor — 404', () => {
  it('does not open a conflict dialog or toast on 404 (callers decide)', async () => {
    server.use(
      http.get('http://localhost/api/projects/missing', () => new HttpResponse(null, { status: 404 })),
    )

    await expect(apiClient.get('http://localhost/api/projects/missing')).rejects.toMatchObject({
      response: { status: 404 },
    })

    const ui = useUiStore.getState()
    expect(ui.conflictDialogOpen).toBe(false)
    expect(ui.activeToast).toBeNull()
  })
})

describe('api-client response interceptor — non-handled statuses pass through', () => {
  it('rejects 500s without side-effecting the UI store', async () => {
    server.use(http.get('http://localhost/api/projects', () => new HttpResponse(null, { status: 500 })))

    await expect(apiClient.get('http://localhost/api/projects')).rejects.toMatchObject({
      response: { status: 500 },
    })

    const ui = useUiStore.getState()
    expect(ui.activeToast).toBeNull()
    expect(ui.conflictDialogOpen).toBe(false)
  })
})
