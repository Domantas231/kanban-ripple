import type { Card, Guid } from '@/lib/types'

export type DueDateFilter = 'overdue' | 'today' | 'week' | 'none'
export type AssigneeStateFilter = 'me' | 'unassigned' | 'multiple'
export type ActivityFilter = '24h' | '7d' | '30d' | 'stale'

export interface ClientCardFilters {
  dueDate: DueDateFilter | null
  assigneeState: AssigneeStateFilter | null
  activity: ActivityFilter | null
  createdByIds: Guid[]
  hasAttachments: boolean | null
  hasComments: boolean | null
  estMin: number | null
  estMax: number | null
}

export const EMPTY_CLIENT_FILTERS: ClientCardFilters = {
  dueDate: null,
  assigneeState: null,
  activity: null,
  createdByIds: [],
  hasAttachments: null,
  hasComments: null,
  estMin: null,
  estMax: null,
}

const MS_PER_DAY = 24 * 60 * 60 * 1000
const STALE_THRESHOLD_DAYS = 30

export function hasActiveClientFilters(filters: ClientCardFilters): boolean {
  return (
    filters.dueDate !== null ||
    filters.assigneeState !== null ||
    filters.activity !== null ||
    filters.createdByIds.length > 0 ||
    filters.hasAttachments !== null ||
    filters.hasComments !== null ||
    filters.estMin !== null ||
    filters.estMax !== null
  )
}

export function countActiveClientFilters(filters: ClientCardFilters): number {
  let count = 0
  if (filters.dueDate !== null) count += 1
  if (filters.assigneeState !== null) count += 1
  if (filters.activity !== null) count += 1
  if (filters.createdByIds.length > 0) count += 1
  if (filters.hasAttachments !== null) count += 1
  if (filters.hasComments !== null) count += 1
  if (filters.estMin !== null || filters.estMax !== null) count += 1
  return count
}

type ApplyOptions = {
  currentUserId?: Guid
  now?: Date
}

export function applyClientCardFilters(
  cards: Card[],
  filters: ClientCardFilters,
  options: ApplyOptions = {},
): Card[] {
  if (!hasActiveClientFilters(filters)) {
    return cards
  }

  const now = options.now ?? new Date()
  const startOfToday = startOfDay(now)
  const endOfToday = new Date(startOfToday.getTime() + MS_PER_DAY)
  const endOfWeek = new Date(startOfToday.getTime() + 7 * MS_PER_DAY)

  return cards.filter((card) => {
    if (filters.dueDate !== null && !matchesDueDate(card, filters.dueDate, startOfToday, endOfToday, endOfWeek)) {
      return false
    }

    if (filters.assigneeState !== null && !matchesAssigneeState(card, filters.assigneeState, options.currentUserId)) {
      return false
    }

    if (filters.activity !== null && !matchesActivity(card, filters.activity, now)) {
      return false
    }

    if (filters.createdByIds.length > 0) {
      if (!card.createdBy || !filters.createdByIds.includes(card.createdBy)) {
        return false
      }
    }

    if (filters.hasAttachments !== null) {
      const has = (card.attachments?.length ?? 0) > 0
      if (has !== filters.hasAttachments) {
        return false
      }
    }

    if (filters.hasComments !== null) {
      const has = (card.comments?.length ?? 0) > 0
      if (has !== filters.hasComments) {
        return false
      }
    }

    if (filters.estMin !== null || filters.estMax !== null) {
      const est = card.estimatedHours
      if (est === null || est === undefined) {
        return false
      }
      if (filters.estMin !== null && est < filters.estMin) {
        return false
      }
      if (filters.estMax !== null && est > filters.estMax) {
        return false
      }
    }

    return true
  })
}

function matchesDueDate(
  card: Card,
  filter: DueDateFilter,
  startOfToday: Date,
  endOfToday: Date,
  endOfWeek: Date,
): boolean {
  if (filter === 'none') {
    return !card.dueDate
  }

  if (!card.dueDate) {
    return false
  }

  const due = new Date(card.dueDate)
  if (Number.isNaN(due.getTime())) {
    return false
  }

  switch (filter) {
    case 'overdue':
      return due < startOfToday
    case 'today':
      return due >= startOfToday && due < endOfToday
    case 'week':
      return due >= startOfToday && due < endOfWeek
    default:
      return false
  }
}

function matchesAssigneeState(card: Card, filter: AssigneeStateFilter, currentUserId: Guid | undefined): boolean {
  const assignments = card.assignments ?? []
  switch (filter) {
    case 'me':
      return Boolean(currentUserId) && assignments.some((assignment) => assignment.userId === currentUserId)
    case 'unassigned':
      return assignments.length === 0
    case 'multiple':
      return assignments.length > 1
    default:
      return false
  }
}

function matchesActivity(card: Card, filter: ActivityFilter, now: Date): boolean {
  const updated = new Date(card.updatedAt)
  if (Number.isNaN(updated.getTime())) {
    return false
  }

  const ageMs = now.getTime() - updated.getTime()

  switch (filter) {
    case '24h':
      return ageMs <= MS_PER_DAY
    case '7d':
      return ageMs <= 7 * MS_PER_DAY
    case '30d':
      return ageMs <= 30 * MS_PER_DAY
    case 'stale':
      return ageMs > STALE_THRESHOLD_DAYS * MS_PER_DAY
    default:
      return false
  }
}

function startOfDay(date: Date): Date {
  const result = new Date(date)
  result.setHours(0, 0, 0, 0)
  return result
}

export interface ClientFilterSearchParams {
  due?: DueDateFilter
  assign?: AssigneeStateFilter
  activity?: ActivityFilter
  createdByIds?: string
  hasAttachments?: '1' | '0'
  hasComments?: '1' | '0'
  estMin?: number
  estMax?: number
}

const DUE_VALUES: readonly DueDateFilter[] = ['overdue', 'today', 'week', 'none']
const ASSIGN_VALUES: readonly AssigneeStateFilter[] = ['me', 'unassigned', 'multiple']
const ACTIVITY_VALUES: readonly ActivityFilter[] = ['24h', '7d', '30d', 'stale']

export function parseClientFiltersFromSearch(search: ClientFilterSearchParams): ClientCardFilters {
  return {
    dueDate: parseEnum(search.due, DUE_VALUES),
    assigneeState: parseEnum(search.assign, ASSIGN_VALUES),
    activity: parseEnum(search.activity, ACTIVITY_VALUES),
    createdByIds: parseCsv(search.createdByIds),
    hasAttachments: parseBool(search.hasAttachments),
    hasComments: parseBool(search.hasComments),
    estMin: parseNumber(search.estMin),
    estMax: parseNumber(search.estMax),
  }
}

export function serializeClientFiltersToSearch(filters: ClientCardFilters): ClientFilterSearchParams {
  return {
    due: filters.dueDate ?? undefined,
    assign: filters.assigneeState ?? undefined,
    activity: filters.activity ?? undefined,
    createdByIds: filters.createdByIds.length > 0 ? filters.createdByIds.join(',') : undefined,
    hasAttachments: filters.hasAttachments === null ? undefined : filters.hasAttachments ? '1' : '0',
    hasComments: filters.hasComments === null ? undefined : filters.hasComments ? '1' : '0',
    estMin: filters.estMin ?? undefined,
    estMax: filters.estMax ?? undefined,
  }
}

function parseEnum<T extends string>(value: string | undefined, allowed: readonly T[]): T | null {
  if (!value) return null
  return (allowed as readonly string[]).includes(value) ? (value as T) : null
}

function parseCsv(value: string | undefined): Guid[] {
  if (!value) return []
  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}

function parseBool(value: string | undefined): boolean | null {
  if (value === '1') return true
  if (value === '0') return false
  return null
}

function parseNumber(value: number | undefined): number | null {
  if (value === undefined || value === null || Number.isNaN(value)) return null
  return value
}
