import { create } from 'zustand'

export type AuthUser = {
  id: string
  email: string
  userName?: string
}

type AuthState = {
  user: AuthUser | null
  accessToken: string | null
  setAuth: (user: AuthUser, accessToken: string) => void
  clearAuth: () => void
  isAuthenticated: boolean
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  setAuth: (user, accessToken) => {
    set({
      user,
      accessToken,
      isAuthenticated: Boolean(accessToken),
    })
  },
  clearAuth: () => {
    set({
      user: null,
      accessToken: null,
      isAuthenticated: false,
    })
  },
  isAuthenticated: false,
}))
