import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'

// Mock @microsoft/signalr identically to the service tests so the singleton
// can be constructed at module load.
const fakeConnection = {
  state: 'Disconnected',
  start: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
  invoke: vi.fn().mockResolvedValue(undefined),
  on: vi.fn(),
  off: vi.fn(),
  onclose: vi.fn(),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
}

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return fakeConnection
    }
  }

  return {
    HubConnectionBuilder,
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Connected: 'Connected',
      Disconnecting: 'Disconnecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: {
      Trace: 0,
      Debug: 1,
      Information: 2,
      Warning: 3,
      Error: 4,
      Critical: 5,
      None: 6,
    },
  }
})

beforeAll(() => {
  ;(globalThis as { __KANBAN_ENABLE_SIGNALR_TESTS?: boolean }).__KANBAN_ENABLE_SIGNALR_TESTS = true
})

beforeEach(() => {
  vi.resetModules()
  fakeConnection.state = 'Disconnected'
  fakeConnection.start.mockClear().mockResolvedValue(undefined)
  fakeConnection.stop.mockClear().mockResolvedValue(undefined)
  fakeConnection.invoke.mockClear().mockResolvedValue(undefined)
})

afterEach(() => {
  vi.doUnmock('@/lib/query-client')
})

describe('signalRLifecycle.initializeSignalRConnectionLifecycle', () => {
  it('connects immediately when the auth store is already authenticated', async () => {
    // Configure auth store BEFORE importing the service so the initial state
    // is observed correctly.
    const { useAuthStore } = await import('@/features/auth')
    useAuthStore
      .getState()
      .setAuth({ id: 'u-1', email: 'user@example.com' }, 'tok')

    const { initializeSignalRConnectionLifecycle } = await import('./signalRLifecycle')
    initializeSignalRConnectionLifecycle()

    // The lifecycle calls connect() synchronously; the start promise is
    // queued, but the call should be observable right away.
    await Promise.resolve()
    expect(fakeConnection.start).toHaveBeenCalled()
  })

  it('does not connect when the auth store is not authenticated', async () => {
    const { useAuthStore } = await import('@/features/auth')
    useAuthStore.getState().clearAuth()

    const { initializeSignalRConnectionLifecycle } = await import('./signalRLifecycle')
    initializeSignalRConnectionLifecycle()

    await Promise.resolve()
    expect(fakeConnection.start).not.toHaveBeenCalled()
  })

  it('reconnects when the user authenticates after init, and disconnects on logout', async () => {
    const { useAuthStore } = await import('@/features/auth')
    useAuthStore.getState().clearAuth()

    const { initializeSignalRConnectionLifecycle } = await import('./signalRLifecycle')
    initializeSignalRConnectionLifecycle()

    // After a real login, the lifecycle should kick off connect().
    useAuthStore.getState().setAuth({ id: 'u-2', email: 'user@example.com' }, 'tok-2')
    await Promise.resolve()
    expect(fakeConnection.start).toHaveBeenCalled()

    // And after logout it should stop the connection. Mark the connection as
    // "Connected" so disconnect() actually invokes stop().
    fakeConnection.state = 'Connected'
    useAuthStore.getState().clearAuth()
    await Promise.resolve()
    expect(fakeConnection.stop).toHaveBeenCalled()
  })
})

describe('SignalRService.joinProject / leaveProject', () => {
  it('joinProject invokes "JoinProject" with the project id', async () => {
    const { useAuthStore } = await import('@/features/auth')
    useAuthStore.getState().setAuth({ id: 'u-1', email: 'user@example.com' }, 'tok')

    const { signalRService } = await import('./signalRService')

    // start() resolves and flips state to Connected.
    fakeConnection.start.mockImplementation(async () => {
      fakeConnection.state = 'Connected'
    })

    await signalRService.joinProject('project-42')

    expect(fakeConnection.invoke).toHaveBeenCalledWith('JoinProject', 'project-42')
  })

  it('leaveProject is a no-op when the connection is not Connected', async () => {
    const { signalRService } = await import('./signalRService')

    fakeConnection.state = 'Disconnected'
    await signalRService.leaveProject('project-42')

    expect(fakeConnection.invoke).not.toHaveBeenCalled()
  })

  it('joinProject throws when start() succeeds but the connection state never reaches Connected', async () => {
    const { useAuthStore } = await import('@/features/auth')
    useAuthStore.getState().setAuth({ id: 'u-1', email: 'user@example.com' }, 'tok')

    const { signalRService } = await import('./signalRService')

    // start resolves but never sets state to Connected — surfaces a clear error.
    fakeConnection.start.mockImplementation(async () => {
      // intentionally leave state as 'Disconnected'
    })

    await expect(signalRService.joinProject('project-1')).rejects.toThrow(/not ready/i)
  })
})
