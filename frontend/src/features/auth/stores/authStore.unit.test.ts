import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from './authStore'

describe('useAuthStore', () => {
  beforeEach(() => {
    useAuthStore.getState().clearAuth()
  })

  it('starts unauthenticated', () => {
    const state = useAuthStore.getState()
    expect(state.user).toBeNull()
    expect(state.accessToken).toBeNull()
    expect(state.isAuthenticated).toBe(false)
  })

  it('sets and clears auth', () => {
    const user = { id: 'u-1', email: 'a@b.com' }
    useAuthStore.getState().setAuth(user, 'token-xyz')

    let state = useAuthStore.getState()
    expect(state.user).toEqual(user)
    expect(state.accessToken).toBe('token-xyz')
    expect(state.isAuthenticated).toBe(true)

    useAuthStore.getState().clearAuth()
    state = useAuthStore.getState()
    expect(state.user).toBeNull()
    expect(state.accessToken).toBeNull()
    expect(state.isAuthenticated).toBe(false)
  })
})
