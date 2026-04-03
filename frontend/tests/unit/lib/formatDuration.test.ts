import { describe, it, expect } from 'vitest'
import { formatDuration } from '@/lib/utils/formatDuration'

describe('formatDuration', () => {
  it('returns "0 min" for zero minutes', () => {
    expect(formatDuration(0)).toBe('0 min')
  })

  it('returns "0 min" for negative values', () => {
    expect(formatDuration(-10)).toBe('0 min')
  })

  it('formats minutes only when under 60', () => {
    expect(formatDuration(30)).toBe('30 min')
    expect(formatDuration(45)).toBe('45 min')
    expect(formatDuration(1)).toBe('1 min')
  })

  it('formats exactly 60 minutes as "1h"', () => {
    expect(formatDuration(60)).toBe('1h')
  })

  it('formats exactly 120 minutes as "2h"', () => {
    expect(formatDuration(120)).toBe('2h')
  })

  it('formats combined hours and minutes', () => {
    expect(formatDuration(90)).toBe('1h 30min')
    expect(formatDuration(75)).toBe('1h 15min')
    expect(formatDuration(150)).toBe('2h 30min')
  })

  it('formats 50 minutes correctly', () => {
    expect(formatDuration(50)).toBe('50 min')
  })

  it('formats 70 minutes correctly', () => {
    expect(formatDuration(70)).toBe('1h 10min')
  })
})
