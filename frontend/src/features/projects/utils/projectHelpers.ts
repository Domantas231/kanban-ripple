import type { ProjectRole } from '@/lib/types'

export function projectRoleLabel(role: ProjectRole): string {
  switch (role) {
    case 0:
      return 'Owner'
    case 1:
      return 'Manager'
    case 2:
      return 'Member'
    default:
      return 'Viewer'
  }
}

export function isManagerPlus(role: ProjectRole | undefined): boolean {
  return role !== undefined && role <= 1
}

export function isMemberPlus(role: ProjectRole | undefined): boolean {
  return role !== undefined && role <= 2
}

export function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())
}
