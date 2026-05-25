import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  flattenSwimlaneCards,
  formatDueDate,
  formatRelativeDate,
  getOverdueCards,
  getTagCounts,
  getTeamWorkload,
  getUnassignedCards,
  getUnassignedTotals,
  getUpcomingCards,
  type FlatCard,
} from './overviewUtils'
import type { BoardSwimlane, Card } from '@/lib/types'

const FROZEN_NOW = new Date('2026-05-05T12:00:00Z')

function makeCard(overrides: Partial<Card> = {}): Card {
  return {
    id: 'c-1',
    columnId: 'col-1',
    title: 'Card',
    position: 1000,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  } as Card
}

function makeFlat(overrides: Partial<FlatCard> = {}): FlatCard {
  return {
    ...(makeCard() as FlatCard),
    boardId: 'b-1',
    boardName: 'Board One',
    columnName: 'Todo',
    ...overrides,
  } as FlatCard
}

describe('flattenSwimlaneCards', () => {
  it('flattens nested boards / columns / cards and tags each card with board+column metadata', () => {
    const swimlanes: BoardSwimlane[] = [
      {
        board: {
          id: 'b-1',
          name: 'Board One',
          projectId: 'p',
          position: 0,
          createdAt: '',
          updatedAt: '',
        },
        columns: [
          {
            column: {
              id: 'col-1',
              boardId: 'b-1',
              name: 'Todo',
              position: 0,
              createdAt: '',
              updatedAt: '',
            },
            cards: [makeCard({ id: 'a' }), makeCard({ id: 'b' })],
            cardCount: 2,
          },
          {
            column: {
              id: 'col-2',
              boardId: 'b-1',
              name: 'Done',
              position: 1,
              createdAt: '',
              updatedAt: '',
            },
            cards: [makeCard({ id: 'c' })],
            cardCount: 1,
          },
        ],
      },
    ]

    const flat = flattenSwimlaneCards(swimlanes)
    expect(flat.map((c) => c.id)).toEqual(['a', 'b', 'c'])
    expect(flat[0].boardName).toBe('Board One')
    expect(flat[0].columnName).toBe('Todo')
    expect(flat[2].columnName).toBe('Done')
  })

  it('handles columns with no cards', () => {
    const swimlanes: BoardSwimlane[] = [
      {
        board: { id: 'b', name: 'B', projectId: 'p', position: 0, createdAt: '', updatedAt: '' },
        columns: [
          {
            column: { id: 'col', boardId: 'b', name: 'Empty', position: 0, createdAt: '', updatedAt: '' },
            cards: [],
            cardCount: 0,
          },
        ],
      },
    ]
    expect(flattenSwimlaneCards(swimlanes)).toEqual([])
  })
})

describe('overdue / upcoming / unassigned filters', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(FROZEN_NOW)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns only overdue cards sorted by dueDate ascending', () => {
    const cards = [
      makeFlat({ id: 'old', dueDate: '2026-05-01T00:00:00Z' }),
      makeFlat({ id: 'older', dueDate: '2026-04-01T00:00:00Z' }),
      makeFlat({ id: 'future', dueDate: '2026-05-10T00:00:00Z' }),
      makeFlat({ id: 'no-date' }),
    ]
    expect(getOverdueCards(cards).map((c) => c.id)).toEqual(['older', 'old'])
  })

  it('returns only cards due within the cutoff window, sorted ascending', () => {
    const cards = [
      makeFlat({ id: 'overdue', dueDate: '2026-05-01T00:00:00Z' }),
      makeFlat({ id: 'today', dueDate: FROZEN_NOW.toISOString() }),
      makeFlat({ id: 'in3d', dueDate: '2026-05-08T12:00:00Z' }),
      makeFlat({ id: 'in10d', dueDate: '2026-05-15T12:00:00Z' }),
    ]
    expect(getUpcomingCards(cards, 7).map((c) => c.id)).toEqual(['today', 'in3d'])
  })

  it('detects unassigned cards', () => {
    const cards = [
      makeFlat({ id: 'a', assignments: [] }),
      makeFlat({ id: 'b' }),
      makeFlat({ id: 'c', assignments: [{ id: 'x', cardId: 'c', userId: 'u', createdAt: '' } as never] }),
    ]
    expect(getUnassignedCards(cards).map((c) => c.id)).toEqual(['a', 'b'])
  })
})

describe('getTeamWorkload', () => {
  it('aggregates cards per assignee with per-board breakdown sorted by card count', () => {
    const heavy = makeFlat({
      id: 'heavy-1',
      boardName: 'B1',
      estimatedHours: 4,
      spentMinutes: 60,
      assignments: [{ id: 'a1', cardId: 'heavy-1', userId: 'u-heavy', user: { id: 'u-heavy', userName: 'Heavy' }, createdAt: '' } as never],
    })
    const heavy2 = makeFlat({
      id: 'heavy-2',
      boardName: 'B2',
      estimatedHours: 2,
      spentMinutes: 30,
      assignments: [{ id: 'a2', cardId: 'heavy-2', userId: 'u-heavy', user: { id: 'u-heavy', userName: 'Heavy' }, createdAt: '' } as never],
    })
    const light = makeFlat({
      id: 'light',
      boardName: 'B1',
      estimatedHours: 1,
      assignments: [{ id: 'a3', cardId: 'light', userId: 'u-light', user: { id: 'u-light', userName: 'Light' }, createdAt: '' } as never],
    })

    const workload = getTeamWorkload([heavy, heavy2, light])

    expect(workload.map((w) => w.userId)).toEqual(['u-heavy', 'u-light'])
    expect(workload[0].cardCount).toBe(2)
    expect(workload[0].estimatedHours).toBe(6)
    expect(workload[0].loggedHours).toBeCloseTo(1.5)
    expect(workload[0].boardBreakdown).toHaveLength(2)
    expect(workload[1].cardCount).toBe(1)
  })
})

describe('getUnassignedTotals', () => {
  it('sums estimated and logged hours for cards without assignees', () => {
    const cards = [
      makeFlat({ id: 'a', estimatedHours: 4, spentMinutes: 90 }),
      makeFlat({ id: 'b', estimatedHours: 2 }),
      makeFlat({
        id: 'c',
        estimatedHours: 9,
        assignments: [{ id: 'x', cardId: 'c', userId: 'u', createdAt: '' } as never],
      }),
    ]
    const totals = getUnassignedTotals(cards)
    expect(totals.cardCount).toBe(2)
    expect(totals.estimatedHours).toBe(6)
    expect(totals.loggedHours).toBeCloseTo(1.5)
  })
})

describe('getTagCounts', () => {
  it('counts cards per tag and aggregates hours, sorted by count desc', () => {
    const cards = [
      makeFlat({
        id: '1',
        estimatedHours: 1,
        cardTags: [{ tag: { id: 't1', name: 'bug', color: '#f00' } }] as never,
      }),
      makeFlat({
        id: '2',
        estimatedHours: 2,
        cardTags: [{ tag: { id: 't1', name: 'bug', color: '#f00' } }] as never,
      }),
      makeFlat({
        id: '3',
        estimatedHours: 3,
        cardTags: [{ tag: { id: 't2', name: 'feat', color: '#0f0' } }] as never,
      }),
    ]
    const counts = getTagCounts(cards)
    expect(counts.map((c) => c.tagId)).toEqual(['t1', 't2'])
    expect(counts[0].count).toBe(2)
    expect(counts[0].estimatedHours).toBe(3)
  })

  it('skips cards with no tags', () => {
    expect(getTagCounts([makeFlat({ id: 'a' })])).toEqual([])
  })
})

describe('formatRelativeDate', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(FROZEN_NOW)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns "just now" for present or future timestamps', () => {
    expect(formatRelativeDate(FROZEN_NOW.toISOString())).toBe('just now')
    expect(formatRelativeDate(new Date(FROZEN_NOW.getTime() + 60_000).toISOString())).toBe('just now')
  })

  it('uses minute / hour / Yesterday / day / week / month boundaries', () => {
    const minus = (ms: number) => new Date(FROZEN_NOW.getTime() - ms).toISOString()
    expect(formatRelativeDate(minus(5 * 60_000))).toBe('5m ago')
    expect(formatRelativeDate(minus(2 * 3_600_000))).toBe('2h ago')
    expect(formatRelativeDate(minus(86_400_000))).toBe('Yesterday')
    expect(formatRelativeDate(minus(3 * 86_400_000))).toBe('3d ago')
    expect(formatRelativeDate(minus(10 * 86_400_000))).toBe('1w ago')
    expect(formatRelativeDate(minus(60 * 86_400_000))).toBe('2mo ago')
  })
})

describe('formatDueDate', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(FROZEN_NOW)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('reports overdue, today, tomorrow, and "in N days"', () => {
    expect(formatDueDate(FROZEN_NOW.toISOString())).toBe('Due today')
    expect(formatDueDate(new Date(FROZEN_NOW.getTime() + 86_400_000).toISOString())).toBe('Due tomorrow')
    expect(formatDueDate(new Date(FROZEN_NOW.getTime() + 5 * 86_400_000).toISOString())).toBe('Due in 5 days')
    expect(formatDueDate(new Date(FROZEN_NOW.getTime() - 86_400_000).toISOString())).toBe('1 day overdue')
    expect(formatDueDate(new Date(FROZEN_NOW.getTime() - 3 * 86_400_000).toISOString())).toBe('3 days overdue')
  })
})
