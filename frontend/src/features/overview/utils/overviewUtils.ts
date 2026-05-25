import type { BoardSwimlane, Card, Guid } from '@/lib/types'

export interface FlatCard extends Card {
  boardId: Guid
  boardName: string
  columnName: string
}


export function flattenSwimlaneCards(boards: BoardSwimlane[]): FlatCard[] {
  const cards: FlatCard[] = []
  for (const boardLane of boards) {
    for (const colLane of boardLane.columns) {
      for (const card of colLane.cards ?? []) {
        cards.push({
          ...card,
          boardId: boardLane.board.id,
          boardName: boardLane.board.name,
          columnName: colLane.column.name,
        })
      }
    }
  }
  return cards
}

export function getOverdueCards(cards: FlatCard[]): FlatCard[] {
  const now = new Date()
  return cards
    .filter((c) => c.dueDate && new Date(c.dueDate) < now)
    .sort((a, b) => new Date(a.dueDate!).getTime() - new Date(b.dueDate!).getTime())
}

export function getUpcomingCards(cards: FlatCard[], days: number): FlatCard[] {
  const now = new Date()
  const cutoff = new Date()
  cutoff.setDate(cutoff.getDate() + days)
  return cards
    .filter((c) => c.dueDate && new Date(c.dueDate) >= now && new Date(c.dueDate) <= cutoff)
    .sort((a, b) => new Date(a.dueDate!).getTime() - new Date(b.dueDate!).getTime())
}

export function getUnassignedCards(cards: FlatCard[]): FlatCard[] {
  return cards.filter((c) => !c.assignments || c.assignments.length === 0)
}

export interface MemberWorkload {
  userId: Guid
  userName: string
  email: string
  cardCount: number
  estimatedHours: number
  loggedHours: number
  boardBreakdown: { boardName: string; count: number; estimatedHours: number; loggedHours: number }[]
}

export function getTeamWorkload(cards: FlatCard[]): MemberWorkload[] {
  const map = new Map<Guid, MemberWorkload>()
  for (const card of cards) {
    const cardEstimated = card.estimatedHours ?? 0
    const cardLogged = (card.spentMinutes ?? 0) / 60
    for (const assignment of card.assignments ?? []) {
      const userId = assignment.userId
      if (!map.has(userId)) {
        map.set(userId, {
          userId,
          userName: assignment.user?.userName?.trim() || '',
          email: assignment.user?.email?.trim() || '',
          cardCount: 0,
          estimatedHours: 0,
          loggedHours: 0,
          boardBreakdown: [],
        })
      }
      const entry = map.get(userId)!
      entry.cardCount += 1
      entry.estimatedHours += cardEstimated
      entry.loggedHours += cardLogged
      const existing = entry.boardBreakdown.find((b) => b.boardName === card.boardName)
      if (existing) {
        existing.count += 1
        existing.estimatedHours += cardEstimated
        existing.loggedHours += cardLogged
      } else {
        entry.boardBreakdown.push({
          boardName: card.boardName,
          count: 1,
          estimatedHours: cardEstimated,
          loggedHours: cardLogged,
        })
      }
    }
  }
  return Array.from(map.values()).sort((a, b) => b.cardCount - a.cardCount)
}

export interface UnassignedTotals {
  cardCount: number
  estimatedHours: number
  loggedHours: number
}

export function getUnassignedTotals(cards: FlatCard[]): UnassignedTotals {
  const unassigned = getUnassignedCards(cards)
  let estimatedHours = 0
  let loggedHours = 0
  for (const card of unassigned) {
    estimatedHours += card.estimatedHours ?? 0
    loggedHours += (card.spentMinutes ?? 0) / 60
  }
  return { cardCount: unassigned.length, estimatedHours, loggedHours }
}

export interface TagCount {
  tagId: Guid
  tagName: string
  tagColor: string
  count: number
  estimatedHours: number
  loggedHours: number
}

export function getTagCounts(cards: FlatCard[]): TagCount[] {
  const map = new Map<Guid, TagCount>()
  for (const card of cards) {
    const cardEstimated = card.estimatedHours ?? 0
    const cardLogged = (card.spentMinutes ?? 0) / 60
    for (const cardTag of card.cardTags ?? []) {
      const tag = cardTag.tag
      if (!tag) continue
      if (!map.has(tag.id)) {
        map.set(tag.id, {
          tagId: tag.id,
          tagName: tag.name,
          tagColor: tag.color,
          count: 0,
          estimatedHours: 0,
          loggedHours: 0,
        })
      }
      const entry = map.get(tag.id)!
      entry.count += 1
      entry.estimatedHours += cardEstimated
      entry.loggedHours += cardLogged
    }
  }
  return Array.from(map.values()).sort((a, b) => b.count - a.count)
}



export function formatRelativeDate(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()

  if (diffMs < 0) return 'just now'

  const diffMinutes = Math.floor(diffMs / 60_000)
  const diffHours = Math.floor(diffMs / 3_600_000)
  const diffDays = Math.floor(diffMs / 86_400_000)

  if (diffMinutes < 1) return 'just now'
  if (diffMinutes < 60) return `${diffMinutes}m ago`
  if (diffHours < 24) return `${diffHours}h ago`
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 7) return `${diffDays}d ago`
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}w ago`
  return `${Math.floor(diffDays / 30)}mo ago`
}

export function formatDueDate(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = date.getTime() - now.getTime()
  const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24))

  if (diffDays < 0) {
    const overdue = Math.abs(diffDays)
    if (overdue === 1) return '1 day overdue'
    return `${overdue} days overdue`
  }
  if (diffDays === 0) return 'Due today'
  if (diffDays === 1) return 'Due tomorrow'
  return `Due in ${diffDays} days`
}
