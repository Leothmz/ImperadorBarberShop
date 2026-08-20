import { describe, it, expect, beforeEach } from 'vitest'
import {
  clampStep,
  clearDraft,
  emptyDraft,
  loadDraft,
  parseDraftDate,
  saveDraft,
  serializeDraftDate,
  type BookingDraft,
} from '@/lib/utils/bookingDraft'
import { mockBarbers } from '../../mocks/handlers'

const full: BookingDraft = {
  step: 4,
  barber: mockBarbers[0],
  serviceIds: ['service-1'],
  date: '2026-08-22',
  slot: '09:00:00',
  clientName: 'Carlos',
  clientPhone: '11999990000',
  notes: 'lateral baixa',
}

describe('clampStep', () => {
  it('keeps the saved step when the draft supports it', () => {
    expect(clampStep(full)).toBe(4)
  })

  it('falls back to step 1 without a barber', () => {
    expect(clampStep({ ...full, barber: null })).toBe(1)
  })

  it('falls back to step 2 without services', () => {
    expect(clampStep({ ...full, serviceIds: [] })).toBe(2)
  })

  it('falls back to step 3 when the slot is missing', () => {
    expect(clampStep({ ...full, slot: null })).toBe(3)
  })

  it('falls back to step 3 when the date is missing', () => {
    expect(clampStep({ ...full, date: null })).toBe(3)
  })
})

describe('draft date round-trip', () => {
  it('survives serialization without shifting the day across timezones', () => {
    const date = new Date(2026, 7, 22)
    const restored = parseDraftDate(serializeDraftDate(date))

    expect(restored?.getFullYear()).toBe(2026)
    expect(restored?.getMonth()).toBe(7)
    expect(restored?.getDate()).toBe(22)
  })

  it('returns null for an absent or malformed date', () => {
    expect(parseDraftDate(null)).toBeNull()
    expect(parseDraftDate('nao-e-data')).toBeNull()
  })
})

describe('session persistence', () => {
  beforeEach(() => {
    window.sessionStorage.clear()
  })

  it('returns null when nothing was saved', () => {
    expect(loadDraft()).toBeNull()
  })

  it('restores a saved draft', () => {
    saveDraft(full)
    const restored = loadDraft()

    expect(restored?.barber?.id).toBe(mockBarbers[0].id)
    expect(restored?.clientPhone).toBe('11999990000')
    expect(restored?.step).toBe(4)
  })

  it('clamps a restored draft that lost its data', () => {
    saveDraft({ ...full, serviceIds: [] })

    expect(loadDraft()?.step).toBe(2)
  })

  it('fills missing keys from an older draft shape', () => {
    window.sessionStorage.setItem('imperador_booking_draft', JSON.stringify({ step: 2 }))

    expect(loadDraft()).toMatchObject({ ...emptyDraft, step: 1 })
  })

  it('survives a corrupted payload instead of throwing', () => {
    window.sessionStorage.setItem('imperador_booking_draft', '{ not json')

    expect(loadDraft()).toBeNull()
  })

  it('clears the draft', () => {
    saveDraft(full)
    clearDraft()

    expect(loadDraft()).toBeNull()
  })
})
