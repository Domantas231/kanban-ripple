import { describe, expect, it } from 'vitest'
import { isManagerPlus, isMemberPlus, isValidEmail, projectRoleLabel } from './projectHelpers'

describe('projectRoleLabel', () => {
  it('labels each known role', () => {
    expect(projectRoleLabel(0)).toBe('Owner')
    expect(projectRoleLabel(1)).toBe('Manager')
    expect(projectRoleLabel(2)).toBe('Member')
    expect(projectRoleLabel(3)).toBe('Viewer')
  })

  it('falls back to Viewer for unknown numeric roles', () => {
    // Backend may grow new roles before the frontend knows about them.
    expect(projectRoleLabel(99 as never)).toBe('Viewer')
  })
})

describe('isManagerPlus', () => {
  it('returns true for owner and manager roles', () => {
    expect(isManagerPlus(0)).toBe(true)
    expect(isManagerPlus(1)).toBe(true)
  })

  it('returns false for member, viewer, and undefined', () => {
    expect(isManagerPlus(2)).toBe(false)
    expect(isManagerPlus(3)).toBe(false)
    expect(isManagerPlus(undefined)).toBe(false)
  })
})

describe('isMemberPlus', () => {
  it('returns true for owner, manager, and member roles', () => {
    expect(isMemberPlus(0)).toBe(true)
    expect(isMemberPlus(1)).toBe(true)
    expect(isMemberPlus(2)).toBe(true)
  })

  it('returns false for viewer and undefined', () => {
    expect(isMemberPlus(3)).toBe(false)
    expect(isMemberPlus(undefined)).toBe(false)
  })
})

describe('isValidEmail', () => {
  it('accepts standard addresses', () => {
    expect(isValidEmail('user@example.com')).toBe(true)
    expect(isValidEmail('first.last+tag@sub.example.co')).toBe(true)
  })

  it('trims surrounding whitespace before validating', () => {
    expect(isValidEmail('  user@example.com  ')).toBe(true)
  })

  it('rejects malformed input', () => {
    expect(isValidEmail('')).toBe(false)
    expect(isValidEmail('user@')).toBe(false)
    expect(isValidEmail('user@example')).toBe(false)
    expect(isValidEmail('@example.com')).toBe(false)
    expect(isValidEmail('user @example.com')).toBe(false)
    expect(isValidEmail('user@example .com')).toBe(false)
    expect(isValidEmail('plainstring')).toBe(false)
  })
})
