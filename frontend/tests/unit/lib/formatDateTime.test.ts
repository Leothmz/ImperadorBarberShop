import { describe, it, expect } from 'vitest'
import {
  formatDate,
  formatTime,
  formatDateTime,
  formatCurrency,
  formatTimeSlot,
  toApiDate,
} from '@/lib/utils/formatDateTime'

describe('formatDate', () => {
  it('formats an ISO date string to DD/MM/YYYY', () => {
    const result = formatDate('2024-03-15T14:30:00.000Z')
    // Allow for timezone differences — just check it contains slashes and numbers
    expect(result).toMatch(/\d{2}\/\d{2}\/\d{4}/)
  })
})

describe('formatTime', () => {
  it('formats to HH:MM format', () => {
    const result = formatTime('2024-03-15T14:30:00.000Z')
    expect(result).toMatch(/^\d{2}:\d{2}$/)
  })
})

describe('formatDateTime', () => {
  it('includes both date and time parts', () => {
    const result = formatDateTime('2024-03-15T14:30:00.000Z')
    expect(result).toContain('às')
    expect(result).toMatch(/\d{2}\/\d{2}\/\d{4}/)
  })
})

describe('formatCurrency', () => {
  it('formats zero correctly', () => {
    expect(formatCurrency(0)).toContain('0')
  })

  it('formats a price with BRL currency symbol', () => {
    const result = formatCurrency(45.9)
    expect(result).toContain('R$')
  })

  it('formats a round number correctly', () => {
    const result = formatCurrency(100)
    expect(result).toContain('R$')
    expect(result).toContain('100')
  })
})

describe('formatTimeSlot', () => {
  it('trims seconds from HH:mm:ss format', () => {
    expect(formatTimeSlot('09:30:00')).toBe('09:30')
  })

  it('trims seconds from HH:mm:ss with different values', () => {
    expect(formatTimeSlot('14:00:00')).toBe('14:00')
  })

  it('handles edge case with single-digit-like slots', () => {
    expect(formatTimeSlot('08:05:00')).toBe('08:05')
  })
})

describe('toApiDate', () => {
  it('formats a Date object to YYYY-MM-DD', () => {
    const date = new Date(2024, 2, 15) // March 15, 2024 (month is 0-indexed)
    expect(toApiDate(date)).toBe('2024-03-15')
  })

  it('pads single-digit months and days', () => {
    const date = new Date(2024, 0, 5) // January 5, 2024
    expect(toApiDate(date)).toBe('2024-01-05')
  })
})
