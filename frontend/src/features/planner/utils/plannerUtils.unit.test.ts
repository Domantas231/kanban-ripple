import { describe, expect, it } from 'vitest'
import {
  blockHeight,
  computeOverlapLayout,
  formatDateParam,
  MINUTES_PER_SLOT,
  SLOT_HEIGHT_PX,
  SLOTS_PER_HOUR,
  TOTAL_SLOTS,
  timeToY,
  yToTime,
} from './plannerUtils'

describe('plannerUtils — coordinate math', () => {
  it('timeToY converts HH:mm into pixel offsets', () => {
    expect(timeToY('00:00')).toBe(0)
    expect(timeToY('01:00')).toBe(SLOT_HEIGHT_PX)
    expect(timeToY('00:30')).toBe(SLOT_HEIGHT_PX / 2)
  })

  it('yToTime snaps to the configured slot interval', () => {
    // 7 minutes from midnight rounds to the closest 15-minute slot (0:00).
    expect(yToTime((7 / 60) * SLOT_HEIGHT_PX)).toBe('00:00:00')
    // 8 minutes from midnight rounds up to 15.
    expect(yToTime((8 / 60) * SLOT_HEIGHT_PX)).toBe('00:15:00')
    expect(yToTime(SLOT_HEIGHT_PX)).toBe('01:00:00')
  })

  it('round-trips times that fall on slot boundaries', () => {
    for (const time of ['00:00', '01:15', '08:30', '12:45', '23:45']) {
      const y = timeToY(time)
      expect(yToTime(y)).toBe(`${time}:00`)
    }
  })

  it('blockHeight matches the difference of timeToY', () => {
    expect(blockHeight('09:00', '10:00')).toBe(SLOT_HEIGHT_PX)
    expect(blockHeight('09:00', '09:15')).toBe(SLOT_HEIGHT_PX / SLOTS_PER_HOUR)
  })

  it('exposes consistent constants', () => {
    expect(SLOTS_PER_HOUR * 24).toBe(TOTAL_SLOTS)
    expect(MINUTES_PER_SLOT * SLOTS_PER_HOUR).toBe(60)
  })
})

describe('formatDateParam', () => {
  it('formats local date as YYYY-MM-DD with zero padding', () => {
    expect(formatDateParam(new Date(2026, 0, 5))).toBe('2026-01-05')
    expect(formatDateParam(new Date(2026, 11, 31))).toBe('2026-12-31')
  })
})

describe('computeOverlapLayout', () => {
  type Block = { id: string; startTime: string; endTime: string }

  it('returns an empty map for no input', () => {
    expect(computeOverlapLayout<Block>([])).toEqual(new Map())
  })

  it('places non-overlapping blocks in column 0 with totalColumns 1', () => {
    const layout = computeOverlapLayout<Block>([
      { id: 'a', startTime: '09:00', endTime: '10:00' },
      { id: 'b', startTime: '10:00', endTime: '11:00' },
    ])
    expect(layout.get('a')).toEqual({ column: 0, totalColumns: 1 })
    expect(layout.get('b')).toEqual({ column: 0, totalColumns: 1 })
  })

  it('places two overlapping blocks side by side in two columns', () => {
    const layout = computeOverlapLayout<Block>([
      { id: 'a', startTime: '09:00', endTime: '10:00' },
      { id: 'b', startTime: '09:30', endTime: '10:30' },
    ])
    expect(layout.get('a')!.totalColumns).toBe(2)
    expect(layout.get('b')!.totalColumns).toBe(2)
    expect(new Set([layout.get('a')!.column, layout.get('b')!.column])).toEqual(new Set([0, 1]))
  })

  it('expands the cluster width to the maximum simultaneous overlap', () => {
    const layout = computeOverlapLayout<Block>([
      { id: 'a', startTime: '09:00', endTime: '12:00' },
      { id: 'b', startTime: '09:30', endTime: '12:00' },
      { id: 'c', startTime: '10:00', endTime: '12:00' },
    ])
    for (const id of ['a', 'b', 'c']) {
      expect(layout.get(id)!.totalColumns).toBe(3)
    }
    expect(new Set(['a', 'b', 'c'].map((id) => layout.get(id)!.column))).toEqual(new Set([0, 1, 2]))
  })

  it('reuses freed columns once an earlier block has ended', () => {
    const layout = computeOverlapLayout<Block>([
      { id: 'a', startTime: '09:00', endTime: '10:00' },
      { id: 'b', startTime: '09:30', endTime: '10:30' },
      // c starts after a ends but before b ends; should reuse a's column.
      { id: 'c', startTime: '10:00', endTime: '11:00' },
    ])
    expect(layout.get('c')!.column).toBe(layout.get('a')!.column)
    // The cluster touches all three (b overlaps both a and c) so width is 2.
    expect(layout.get('a')!.totalColumns).toBe(2)
    expect(layout.get('c')!.totalColumns).toBe(2)
  })

  it('isolates clusters that do not touch', () => {
    const layout = computeOverlapLayout<Block>([
      { id: 'a', startTime: '09:00', endTime: '10:00' },
      { id: 'b', startTime: '09:30', endTime: '10:30' },
      { id: 'c', startTime: '14:00', endTime: '15:00' },
    ])
    expect(layout.get('a')!.totalColumns).toBe(2)
    expect(layout.get('c')!.totalColumns).toBe(1)
  })
})
