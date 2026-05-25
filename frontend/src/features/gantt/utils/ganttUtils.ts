import type { Card } from '@/lib/types'

export function getDaysBetween(start: Date, end: Date): number {
  const diffMs = end.getTime() - start.getTime()
  return Math.ceil(diffMs / (1000 * 60 * 60 * 24))
}

export function formatDateShort(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function generateDays(start: Date, end: Date): Date[] {
  const days: Date[] = []
  const current = new Date(start)
  while (current <= end) {
    days.push(new Date(current))
    current.setDate(current.getDate() + 1)
  }
  return days
}

const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export function groupDaysByMonth(days: Date[]): Array<{ label: string; count: number }> {
  const groups: Array<{ label: string; count: number }> = []
  for (const day of days) {
    const label = `${MONTH_LABELS[day.getMonth()]} ${day.getFullYear()}`
    const last = groups[groups.length - 1]
    if (last && last.label === label) {
      last.count++
    } else {
      groups.push({ label, count: 1 })
    }
  }
  return groups
}

export function getBarColor(card: Card): string {
  if (card.cardTags && card.cardTags.length > 0) {
    const firstTag = card.cardTags[0]
    if (firstTag.tag?.color) return firstTag.tag.color
  }
  return ''
}

/** Type guard that narrows Card to one with non-null start/due dates */
export function hasScheduleDates(card: Card): card is Card & { startDate: string; dueDate: string } {
  return Boolean(card.startDate && card.dueDate)
}
