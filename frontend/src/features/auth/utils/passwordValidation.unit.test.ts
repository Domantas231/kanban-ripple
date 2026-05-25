import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import {
  extractErrorMessage,
  getPasswordStrengthColor,
  getPasswordStrengthScore,
  passwordRequirements,
} from './passwordValidation'

describe('getPasswordStrengthScore', () => {
  it('returns 0 for an empty password', () => {
    expect(getPasswordStrengthScore('')).toBe(0)
  })

  it('counts each satisfied requirement', () => {
    // Length only
    expect(getPasswordStrengthScore('abcdefgh')).toBe(2) // length + lowercase
    // Length + upper + lower
    expect(getPasswordStrengthScore('Abcdefgh')).toBe(3)
    // Length + upper + lower + digit
    expect(getPasswordStrengthScore('Abcdefg1')).toBe(4)
    // All five
    expect(getPasswordStrengthScore('Abcdefg1!')).toBe(5)
  })

  it('does not exceed the requirement count', () => {
    expect(getPasswordStrengthScore('SuperSecure!1')).toBeLessThanOrEqual(passwordRequirements.length)
  })
})

describe('getPasswordStrengthColor', () => {
  it.each([
    [0, 'error'],
    [1, 'error'],
    [2, 'error'],
    [3, 'warning'],
    [4, 'warning'],
    [5, 'primary'],
  ] as const)('maps score %i to %s', (score, expected) => {
    expect(getPasswordStrengthColor(score)).toBe(expected)
  })
})

describe('extractErrorMessage', () => {
  it('returns the server-provided message when present', () => {
    const headers = new AxiosHeaders()
    const err = new AxiosError(
      'Request failed',
      '400',
      { headers } as never,
      undefined,
      {
        status: 400,
        statusText: 'Bad Request',
        data: { message: 'Email already taken' },
        headers,
        config: { headers } as never,
      },
    )

    expect(extractErrorMessage(err, 'fallback')).toBe('Email already taken')
  })

  it('reads the project error envelope shape { error: { message } }', () => {
    const headers = new AxiosHeaders()
    const err = new AxiosError(
      'Request failed',
      '409',
      { headers } as never,
      undefined,
      {
        status: 409,
        statusText: 'Conflict',
        data: {
          error: {
            code: 'DUPLICATE_NAME',
            message: "Display name 'Alice' is already taken.",
          },
        },
        headers,
        config: { headers } as never,
      },
    )

    expect(extractErrorMessage(err, 'fallback')).toBe("Display name 'Alice' is already taken.")
  })

  it('falls back to the provided message when no axios payload exists', () => {
    expect(extractErrorMessage(new Error('boom'), 'fallback')).toBe('fallback')
    expect(extractErrorMessage(undefined, 'fallback')).toBe('fallback')
    expect(extractErrorMessage(null, 'fallback')).toBe('fallback')
  })
})
