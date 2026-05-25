import type { AxiosError } from 'axios'

export const passwordRequirements = [
  { label: 'At least 8 characters', test: (pw: string) => pw.length >= 8 },
  { label: 'One uppercase letter', test: (pw: string) => /[A-Z]/.test(pw) },
  { label: 'One lowercase letter', test: (pw: string) => /[a-z]/.test(pw) },
  { label: 'One digit', test: (pw: string) => /[0-9]/.test(pw) },
  { label: 'One special character', test: (pw: string) => /[^a-zA-Z0-9]/.test(pw) },
] as const

export function getPasswordStrengthScore(password: string): number {
  if (!password) return 0
  return passwordRequirements.filter((req) => req.test(password)).length
}

export function getPasswordStrengthColor(score: number): 'error' | 'warning' | 'primary' {
  if (score <= 2) return 'error'
  if (score <= 4) return 'warning'
  return 'primary'
}

export function extractErrorMessage(err: unknown, fallback: string): string {
  const axiosErr = err as AxiosError<{ error?: { message?: string }; message?: string }>
  return (
    axiosErr?.response?.data?.error?.message ??
    axiosErr?.response?.data?.message ??
    fallback
  )
}
