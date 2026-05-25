import { describe, expect, it } from 'vitest'
import {
  EMPTY_CLIENT_FILTERS,
  applyClientCardFilters,
  countActiveClientFilters,
  hasActiveClientFilters,
  parseClientFiltersFromSearch,
  serializeClientFiltersToSearch,
} from './cardFilters'
import type { Card } from '@/lib/types'

function makeCard(overrides: Partial<Card> = {}): Card {
  return {
    id: overrides.id ?? 'card-1',
    columnId: 'col-1',
    title: 'Test card',
    position: 1000,
    version: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  } as Card
}

describe('cardFilters', () => {
  it('returns input list unchanged when no filters are active', () => {
    const cards = [makeCard()]
    expect(applyClientCardFilters(cards, EMPTY_CLIENT_FILTERS)).toBe(cards)
    expect(hasActiveClientFilters(EMPTY_CLIENT_FILTERS)).toBe(false)
    expect(countActiveClientFilters(EMPTY_CLIENT_FILTERS)).toBe(0)
  })

  it('filters overdue cards by due date', () => {
    const now = new Date('2026-05-10T12:00:00Z')
    const cards = [
      makeCard({ id: 'a', dueDate: '2026-05-01T00:00:00Z' }),
      makeCard({ id: 'b', dueDate: '2026-05-15T00:00:00Z' }),
      makeCard({ id: 'c', dueDate: null }),
    ]

    const result = applyClientCardFilters(
      cards,
      { ...EMPTY_CLIENT_FILTERS, dueDate: 'overdue' },
      { now },
    )

    expect(result.map((c) => c.id)).toEqual(['a'])
  })

  it('filters cards assigned to the current user', () => {
    const cards = [
      makeCard({ id: 'a', assignments: [{ id: 'x', cardId: 'a', userId: 'me', createdAt: '' } as never] }),
      makeCard({ id: 'b', assignments: [] }),
    ]

    const result = applyClientCardFilters(
      cards,
      { ...EMPTY_CLIENT_FILTERS, assigneeState: 'me' },
      { currentUserId: 'me' },
    )

    expect(result.map((c) => c.id)).toEqual(['a'])
  })

  it('counts active filters', () => {
    expect(
      countActiveClientFilters({
        ...EMPTY_CLIENT_FILTERS,
        dueDate: 'today',
        assigneeState: 'me',
        hasAttachments: true,
      }),
    ).toBe(3)
  })

  it('round-trips through search params', () => {
    const filters = {
      ...EMPTY_CLIENT_FILTERS,
      dueDate: 'week' as const,
      assigneeState: 'unassigned' as const,
      hasComments: true,
      estMin: 1,
      estMax: 8,
    }
    const search = serializeClientFiltersToSearch(filters)
    expect(parseClientFiltersFromSearch(search)).toEqual(filters)
  })
})
