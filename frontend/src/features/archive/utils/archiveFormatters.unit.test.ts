import { describe, expect, it } from 'vitest'
import { formatDeletedAt, isMemberPlus } from './archiveFormatters'

describe('formatDeletedAt', () => {
  it('returns "Unknown" for null or empty values', () => {
    expect(formatDeletedAt(undefined)).toBe('Unknown')
    expect(formatDeletedAt(null)).toBe('Unknown')
    expect(formatDeletedAt('')).toBe('Unknown')
  })

  it('formats ISO timestamps as YYYY-MM-DD', () => {
    expect(formatDeletedAt('2026-03-09T12:34:56Z')).toBe('2026-03-09')
  })

  it('zero-pads single-digit months and days', () => {
    expect(formatDeletedAt('2026-01-05T00:00:00Z')).toBe('2026-01-05')
  })

  it('returns the raw value when the date cannot be parsed', () => {
    expect(formatDeletedAt('not a date')).toBe('not a date')
  })
})

describe('isMemberPlus', () => {
  it('returns true for Owner / Manager / Member', () => {
    expect(isMemberPlus(0)).toBe(true)
    expect(isMemberPlus(1)).toBe(true)
    expect(isMemberPlus(2)).toBe(true)
  })

  it('returns false for Viewer', () => {
    expect(isMemberPlus(3)).toBe(false)
  })

  it('returns false when the role is missing', () => {
    expect(isMemberPlus(undefined)).toBe(false)
  })
})
