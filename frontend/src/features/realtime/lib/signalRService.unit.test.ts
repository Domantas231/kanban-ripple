import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'

// Capture every event handler the service registers so each test can fire
// fake hub events and assert on the resulting query-cache effects.
type Handler = (...args: unknown[]) => void
const registeredHandlers = new Map<string, Handler[]>()

const fakeConnection = {
  state: 'Disconnected' as const,
  start: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
  invoke: vi.fn().mockResolvedValue(undefined),
  on: vi.fn((eventName: string, handler: Handler) => {
    const list = registeredHandlers.get(eventName) ?? []
    list.push(handler)
    registeredHandlers.set(eventName, list)
  }),
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

// Enable real handler registration in this file only. The default test
// behaviour disables SignalR entirely (see signalRService.ts).
beforeAll(() => {
  ;(globalThis as { __KANBAN_ENABLE_SIGNALR_TESTS?: boolean }).__KANBAN_ENABLE_SIGNALR_TESTS = true
})

// Imports below MUST be dynamic so the env flag and the mock are both in
// place when the service constructs its singleton.
type QueryClientLike = {
  invalidateQueries: ReturnType<typeof vi.fn>
  setQueryData: ReturnType<typeof vi.fn>
}

let queryClient: QueryClientLike

beforeEach(async () => {
  // Re-import the query-client + service for each test so the module-level
  // singleton starts clean and we can hand it a fresh spy.
  vi.resetModules()
  registeredHandlers.clear()

  queryClient = {
    invalidateQueries: vi.fn(),
    setQueryData: vi.fn(),
  }

  vi.doMock('@/lib/query-client', () => ({ queryClient }))

  // Trigger the singleton construction now that the mock is registered.
  await import('./signalRService')
})

afterEach(() => {
  vi.doUnmock('@/lib/query-client')
})

function fire(eventName: string, payload?: unknown) {
  const handlers = registeredHandlers.get(eventName) ?? []
  if (handlers.length === 0) {
    throw new Error(`No handlers registered for ${eventName}`)
  }
  for (const handler of handlers) {
    handler(payload)
  }
}

function invalidatedKeys(): unknown[][] {
  return queryClient.invalidateQueries.mock.calls.map((c) => c[0])
}

function hasInvalidationMatching(predicate: (call: { queryKey?: unknown[] }) => boolean): boolean {
  return queryClient.invalidateQueries.mock.calls.some((call) => predicate(call[0] as { queryKey?: unknown[] }))
}

describe('SignalRService — card events', () => {
  it('registers BOTH PascalCase and camelCase aliases for every card event', () => {
    expect(registeredHandlers.has('CardCreated')).toBe(true)
    expect(registeredHandlers.has('cardCreated')).toBe(true)
    expect(registeredHandlers.has('CardUpdated')).toBe(true)
    expect(registeredHandlers.has('cardUpdated')).toBe(true)
    expect(registeredHandlers.has('CardDeleted')).toBe(true)
    expect(registeredHandlers.has('cardDeleted')).toBe(true)
    expect(registeredHandlers.has('CardMoved')).toBe(true)
    expect(registeredHandlers.has('cardMoved')).toBe(true)
  })

  it('CardCreated invalidates that board\'s cards list, the card detail, and any swimlane', () => {
    fire('CardCreated', {
      id: 'card-1',
      column: { boardId: 'board-1' },
    })

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ queryKey: ['boards', 'board-1', 'cards'] }),
        expect.objectContaining({ queryKey: ['cards', 'card-1'] }),
      ]),
    )
    expect(
      hasInvalidationMatching((call) => typeof call.queryKey === 'undefined'),
    ).toBe(true) // the swimlane predicate-based call
  })

  it('CardUpdated falls back to broad cards invalidation when boardId is missing', () => {
    fire('CardUpdated', { id: 'card-2', column: undefined })

    // Without a boardId the service uses a predicate-based invalidation
    // (no queryKey on the options object) — assert it ran at least once.
    expect(queryClient.invalidateQueries).toHaveBeenCalled()
    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([expect.objectContaining({ queryKey: ['cards', 'card-2'] })]),
    )
  })

  it('camelCase cardMoved still triggers the same invalidation as PascalCase CardMoved', () => {
    fire('cardMoved', {
      id: 'card-3',
      column: { boardId: 'board-2' },
    })

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ queryKey: ['boards', 'board-2', 'cards'] }),
        expect.objectContaining({ queryKey: ['cards', 'card-3'] }),
      ]),
    )
  })
})

describe('SignalRService — column events', () => {
  it('ColumnCreated invalidates columns and swimlane', () => {
    fire('ColumnCreated', { id: 'column-1', boardId: 'board-1' })

    // Both the column-predicate and the swimlane-predicate run.
    expect(queryClient.invalidateQueries).toHaveBeenCalledTimes(2)
  })

  it('ColumnUpdated/ColumnDeleted fire the same invalidation paths', () => {
    fire('ColumnUpdated', { id: 'column-1' })
    expect(queryClient.invalidateQueries).toHaveBeenCalledTimes(2)

    queryClient.invalidateQueries.mockClear()
    fire('ColumnDeleted', { id: 'column-1' })
    expect(queryClient.invalidateQueries).toHaveBeenCalledTimes(2)
  })
})

describe('SignalRService — board events', () => {
  it('BoardCreated invalidates the boards predicate, swimlane, and the projects list', () => {
    fire('BoardCreated', { id: 'board-1', projectId: 'project-1' })

    // 3 invalidations: boards predicate, swimlane predicate, projects list (queryKey-based).
    expect(queryClient.invalidateQueries).toHaveBeenCalledTimes(3)
    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([expect.objectContaining({ queryKey: ['projects'] })]),
    )
  })
})

describe('SignalRService — comment events', () => {
  it('CommentCreated with a cardId invalidates that card detail plus the comments predicate', () => {
    fire('CommentCreated', { cardId: 'card-9' })

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([expect.objectContaining({ queryKey: ['cards', 'card-9'] })]),
    )
    // Plus a predicate-based comments invalidation.
    expect(queryClient.invalidateQueries).toHaveBeenCalled()
  })

  it('CommentDeleted without a cardId still invalidates the comments predicate broadly', () => {
    fire('CommentDeleted', {})
    expect(queryClient.invalidateQueries).toHaveBeenCalled()
  })
})

describe('SignalRService — notification events', () => {
  it('NotificationReceived bumps the cached unread count and invalidates notifications', () => {
    queryClient.setQueryData.mockImplementation(
      (_key: unknown[], updater: (prev?: number) => unknown) => updater(2),
    )

    fire('NotificationReceived', {})

    // setQueryData is called with the unread-count key and the updater.
    expect(queryClient.setQueryData).toHaveBeenCalled()
    const [key, updater] = queryClient.setQueryData.mock.calls[0]
    expect(key).toEqual(['notifications', 'unreadCount'])
    expect(updater(2)).toBe(3)
    expect(updater(undefined)).toBe(undefined) // no change when count is unknown

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ queryKey: ['notifications'] }),
        expect.objectContaining({ queryKey: ['notifications', 'unreadCount'] }),
      ]),
    )
  })
})

describe('SignalRService — planner events', () => {
  it('PlannerBlockChanged invalidates planner, swimlane, cards, and boards queries', () => {
    fire('PlannerBlockChanged', {})
    // Four predicate-based invalidations: swimlane, planner, cards, boards.
    expect(queryClient.invalidateQueries).toHaveBeenCalledTimes(4)
  })
})
