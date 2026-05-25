export type CardMeta = {
  columnName: string
  columnIndex: number
  isLastColumn: boolean
}

// Semantic column palette: visually distinct hues for kanban columns
export const COLUMN_COLORS = [
  '#3B82F6', // blue
  '#8B5CF6', // violet
  '#F59E0B', // amber
  '#10B981', // emerald
  '#06B6D4', // cyan
  '#EC4899', // pink
  '#6366F1', // indigo
  '#14B8A6', // teal
]

export const WEEKDAY_LABELS = ['S', 'M', 'T', 'W', 'T', 'F', 'S']

export const BAR_HEIGHT = 32
export const BAR_GAP = 4
export const ROW_HEIGHT = BAR_HEIGHT + BAR_GAP
export const HEADER_HEIGHT = 60
export const CLICK_THRESHOLD = 4
export const SCROLL_EXTEND_THRESHOLD = 300
