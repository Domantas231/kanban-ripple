export const PLANNER_START_HOUR = 0
export const PLANNER_END_HOUR = 24
export const SLOT_HEIGHT_PX = 60
export const MINUTES_PER_SLOT = 15
export const SLOTS_PER_HOUR = 60 / MINUTES_PER_SLOT
export const TOTAL_HOURS = PLANNER_END_HOUR - PLANNER_START_HOUR
export const TOTAL_SLOTS = TOTAL_HOURS * SLOTS_PER_HOUR
export const ROW_HEIGHT_PX = SLOT_HEIGHT_PX / SLOTS_PER_HOUR
export const TIMELINE_HEIGHT_PX = TOTAL_HOURS * SLOT_HEIGHT_PX

export const HOUR_LABELS: string[] = Array.from(
  { length: TOTAL_HOURS + 1 },
  (_, i) => {
    const hour = PLANNER_START_HOUR + i
    return `${hour.toString().padStart(2, '0')}:00`
  },
)

/** Convert "HH:mm" time string to pixel offset from timeline top. */
export function timeToY(time: string): number {
  const [h, m] = time.split(':').map(Number)
  const minutesFromStart = (h - PLANNER_START_HOUR) * 60 + m
  return (minutesFromStart / 60) * SLOT_HEIGHT_PX
}

/** Inverse of timeToY, snapped to MINUTES_PER_SLOT. Returns "HH:mm:ss". */
export function yToTime(y: number): string {
  const totalMinutes = (y / SLOT_HEIGHT_PX) * 60
  const snapped = Math.round(totalMinutes / MINUTES_PER_SLOT) * MINUTES_PER_SLOT
  const h = PLANNER_START_HOUR + Math.floor(snapped / 60)
  const m = snapped % 60
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:00`
}

export function blockHeight(startTime: string, endTime: string): number {
  return timeToY(endTime) - timeToY(startTime)
}

export function getBrowserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone
}

export function formatDateParam(date: Date): string {
  const y = date.getFullYear()
  const m = (date.getMonth() + 1).toString().padStart(2, '0')
  const d = date.getDate().toString().padStart(2, '0')
  return `${y}-${m}-${d}`
}

function timeToMinutes(time: string): number {
  const [h, m] = time.split(':').map(Number)
  return h * 60 + m
}

/** Current wall-clock time as pixel offset from timeline top. */
export function currentTimeY(): number | null {
  const now = new Date()
  const h = now.getHours()
  const m = now.getMinutes()
  const minutesFromStart = (h - PLANNER_START_HOUR) * 60 + m
  return (minutesFromStart / 60) * SLOT_HEIGHT_PX
}

/**
 * Google-Calendar-style overlap layout.
 * Assigns each block a `column` index and `totalColumns` count so they
 * can be rendered side-by-side when they overlap in time.
 */
export type OverlapLayout = {
  column: number
  totalColumns: number
}

export function computeOverlapLayout<T extends { startTime: string; endTime: string; id: string }>(
  blocks: T[],
): Map<string, OverlapLayout> {
  if (blocks.length === 0) return new Map()

  const sorted = [...blocks].sort((a, b) => timeToMinutes(a.startTime) - timeToMinutes(b.startTime))

  // Build clusters of overlapping blocks
  const clusters: T[][] = []
  let currentCluster: T[] = [sorted[0]]
  let clusterEnd = timeToMinutes(sorted[0].endTime)

  for (let i = 1; i < sorted.length; i++) {
    const blockStart = timeToMinutes(sorted[i].startTime)
    if (blockStart < clusterEnd) {
      currentCluster.push(sorted[i])
      clusterEnd = Math.max(clusterEnd, timeToMinutes(sorted[i].endTime))
    } else {
      clusters.push(currentCluster)
      currentCluster = [sorted[i]]
      clusterEnd = timeToMinutes(sorted[i].endTime)
    }
  }
  clusters.push(currentCluster)

  const result = new Map<string, OverlapLayout>()

  for (const cluster of clusters) {
    // Greedy column assignment
    const columns: number[] = new Array(cluster.length).fill(0)
    const columnEnds: number[] = [] // tracks the end-minute of each column

    for (let i = 0; i < cluster.length; i++) {
      const start = timeToMinutes(cluster[i].startTime)
      let placed = false
      for (let c = 0; c < columnEnds.length; c++) {
        if (start >= columnEnds[c]) {
          columns[i] = c
          columnEnds[c] = timeToMinutes(cluster[i].endTime)
          placed = true
          break
        }
      }
      if (!placed) {
        columns[i] = columnEnds.length
        columnEnds.push(timeToMinutes(cluster[i].endTime))
      }
    }

    const totalColumns = columnEnds.length
    for (let i = 0; i < cluster.length; i++) {
      result.set(cluster[i].id, { column: columns[i], totalColumns })
    }
  }

  return result
}
