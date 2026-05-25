import { describe, expect, it } from 'vitest'
import {
  formatDateShort,
  generateDays,
  getBarColor,
  getDaysBetween,
  groupDaysByMonth,
  hasScheduleDates,
} from './ganttUtils'
import type { Card } from '@/lib/types'

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

describe('getDaysBetween', () => {
  it('returns 0 for the same instant', () => {
    const d = new Date('2026-05-05T00:00:00Z')
    expect(getDaysBetween(d, d)).toBe(0)
  })

  it('returns whole days, rounding up partials', () => {
    const start = new Date('2026-05-05T00:00:00Z')
    expect(getDaysBetween(start, new Date('2026-05-06T00:00:00Z'))).toBe(1)
    // 25h gap rounds up to 2 days because of Math.ceil.
    expect(getDaysBetween(start, new Date('2026-05-06T01:00:00Z'))).toBe(2)
  })
})

describe('formatDateShort', () => {
  it('formats as YYYY-MM-DD with zero padding', () => {
    expect(formatDateShort(new Date(2026, 0, 5))).toBe('2026-01-05')
    expect(formatDateShort(new Date(2026, 11, 31))).toBe('2026-12-31')
  })
})

describe('generateDays', () => {
  it('returns one entry per day inclusive of start and end', () => {
    const days = generateDays(new Date('2026-05-05T12:00:00Z'), new Date('2026-05-07T12:00:00Z'))
    expect(days).toHaveLength(3)
  })

  it('returns a single day when start equals end', () => {
    const start = new Date('2026-05-05T00:00:00Z')
    expect(generateDays(start, start)).toHaveLength(1)
  })

  it('does not mutate the input start date', () => {
    const start = new Date('2026-05-05T00:00:00Z')
    const before = start.toISOString()
    generateDays(start, new Date('2026-05-09T00:00:00Z'))
    expect(start.toISOString()).toBe(before)
  })
})

describe('groupDaysByMonth', () => {
  it('coalesces consecutive days that share a month into a single group', () => {
    const days = [
      new Date(2026, 0, 30),
      new Date(2026, 0, 31),
      new Date(2026, 1, 1),
      new Date(2026, 1, 2),
    ]
    expect(groupDaysByMonth(days)).toEqual([
      { label: 'Jan 2026', count: 2 },
      { label: 'Feb 2026', count: 2 },
    ])
  })

  it('returns an empty array for empty input', () => {
    expect(groupDaysByMonth([])).toEqual([])
  })
})

describe('hasScheduleDates', () => {
  it('returns true only when both startDate and dueDate are set', () => {
    expect(hasScheduleDates(makeCard({ startDate: '2026-05-01T00:00:00Z', dueDate: '2026-05-09T00:00:00Z' }))).toBe(true)
    expect(hasScheduleDates(makeCard({ startDate: '2026-05-01T00:00:00Z' }))).toBe(false)
    expect(hasScheduleDates(makeCard({ dueDate: '2026-05-09T00:00:00Z' }))).toBe(false)
    expect(hasScheduleDates(makeCard({}))).toBe(false)
  })
})

describe('getBarColor', () => {
  it('returns the first tag color when present', () => {
    const card = makeCard({
      cardTags: [
        { tag: { id: 't1', name: 'red', color: '#ff0000' } },
        { tag: { id: 't2', name: 'blue', color: '#0000ff' } },
      ] as never,
    })
    expect(getBarColor(card)).toBe('#ff0000')
  })

  it('returns an empty string when no tags exist', () => {
    expect(getBarColor(makeCard({ cardTags: [] }))).toBe('')
    expect(getBarColor(makeCard({}))).toBe('')
  })
})
