import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { cleanup } from '@testing-library/react'
import { server } from './msw/server'
import { useAuthStore } from '@/features/auth'
import { useUiStore } from '@/stores/uiStore'

// IntersectionObserver mock for jsdom
class IntersectionObserverMock {
  callback: IntersectionObserverCallback
  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback
  }
  observe() {}
  unobserve() {}
  disconnect() {}
}
Object.defineProperty(window, 'IntersectionObserver', {
  writable: true,
  value: IntersectionObserverMock,
})

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

afterEach(() => {
  cleanup()
  server.resetHandlers()
  useAuthStore.getState().clearAuth()
  // The UI store is module-level singleton — toasts and the conflict dialog
  // would otherwise leak between tests when an interceptor (e.g. 409) fires.
  const ui = useUiStore.getState()
  ui.dismissToast()
  ui.closeConflictDialog()
})

afterAll(() => {
  server.close()
})
