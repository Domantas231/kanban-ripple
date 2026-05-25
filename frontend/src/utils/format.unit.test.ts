import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { timeAgo } from './format'

const FROZEN_NOW = new Date('2026-05-05T12:00:00Z')

describe('timeAgo', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(FROZEN_NOW)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  function minutesAgo(n: number): string {
    return new Date(FROZEN_NOW.getTime() - n * 60_000).toISOString()
  }

  function hoursAgo(n: number): string {
    return new Date(FROZEN_NOW.getTime() - n * 3_600_000).toISOString()
  }

  function daysAgo(n: number): string {
    return new Date(FROZEN_NOW.getTime() - n * 86_400_000).toISOString()
  }

  it('returns "just now" for sub-minute differences', () => {
    expect(timeAgo(minutesAgo(0))).toBe('just now')
    // 30s ago is still under a minute.
    expect(timeAgo(new Date(FROZEN_NOW.getTime() - 30_000).toISOString())).toBe('just now')
  })

  it('reports minutes for sub-hour differences', () => {
    expect(timeAgo(minutesAgo(1))).toBe('1m ago')
    expect(timeAgo(minutesAgo(59))).toBe('59m ago')
  })

  it('reports hours for sub-day differences', () => {
    expect(timeAgo(hoursAgo(1))).toBe('1h ago')
    expect(timeAgo(hoursAgo(23))).toBe('23h ago')
  })

  it('reports "yesterday" for exactly one day', () => {
    expect(timeAgo(daysAgo(1))).toBe('yesterday')
  })

  it('reports days for older differences', () => {
    expect(timeAgo(daysAgo(2))).toBe('2d ago')
    expect(timeAgo(daysAgo(30))).toBe('30d ago')
  })
})
