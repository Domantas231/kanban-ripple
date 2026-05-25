import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { useAuthStore } from '../stores/authStore'
import { isAuthenticated, redirectIfAuthenticated, requireAuthenticated } from './guards'
import { server } from '@/testing/msw/server'

beforeEach(() => {
  useAuthStore.getState().clearAuth()
})

afterEach(() => {
  useAuthStore.getState().clearAuth()
})

const authResult = {
  userId: '11111111-1111-1111-1111-111111111111',
  email: 'user@example.com',
  accessToken: 'tok',
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  refreshToken: 'r',
  refreshTokenExpiresAt: '2030-01-08T00:00:00Z',
}

describe('isAuthenticated', () => {
  it('returns false when the auth store has no token', () => {
    expect(isAuthenticated()).toBe(false)
  })

  it('returns true once setAuth populates the store', () => {
    useAuthStore.getState().setAuth({ id: 'u', email: 'e' }, 'tok')
    expect(isAuthenticated()).toBe(true)
  })
})

describe('redirectIfAuthenticated', () => {
  it('throws a redirect Response when the store is already authenticated', async () => {
    useAuthStore.getState().setAuth({ id: 'u', email: 'e' }, 'tok')

    await expect(redirectIfAuthenticated()).rejects.toBeDefined()
    // TanStack Router's redirect() throws a Response-like object with status 307.
    const err = await redirectIfAuthenticated().catch((e) => e)
    expect(err).toBeDefined()
    expect((err as { status?: number }).status).toBe(307)
  })

  it('throws a redirect when the silent refresh succeeds', async () => {
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(authResult, { status: 200 })))

    const err = await redirectIfAuthenticated('/projects').catch((e) => e)
    expect((err as { status?: number }).status).toBe(307)
  })

  it('resolves silently when neither the store nor the refresh succeed', async () => {
    server.use(http.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })))

    // Should NOT throw — caller treats this as "render the public route".
    await expect(redirectIfAuthenticated()).resolves.toBeUndefined()
  })
})

describe('requireAuthenticated', () => {
  it('returns silently when already authenticated', async () => {
    useAuthStore.getState().setAuth({ id: 'u', email: 'e' }, 'tok')

    await expect(requireAuthenticated('/projects')).resolves.toBeUndefined()
  })

  it('throws a redirect to /login when refresh fails', async () => {
    server.use(http.post('*/api/auth/refresh', () => new HttpResponse(null, { status: 401 })))

    const err = await requireAuthenticated('/projects/abc').catch((e) => e)
    expect((err as { status?: number }).status).toBe(307)
  })

  it('does not throw when the silent refresh succeeds', async () => {
    server.use(http.post('*/api/auth/refresh', () => HttpResponse.json(authResult, { status: 200 })))

    await expect(requireAuthenticated('/projects')).resolves.toBeUndefined()
  })
})
