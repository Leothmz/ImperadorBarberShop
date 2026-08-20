import { describe, it, expect } from 'vitest'
import { buildAppointmentIcs } from '@/lib/utils/ics'

const base = {
  scheduledAt: '2026-08-22T09:00:00',
  durationMinutes: 50,
  barberName: 'João Silva',
  serviceNames: ['Corte', 'Barba'],
}

describe('buildAppointmentIcs', () => {
  it('keeps wall-clock time — a UTC conversion would move the appointment', () => {
    const ics = buildAppointmentIcs(base)

    expect(ics).toContain('DTSTART:20260822T090000')
    expect(ics).not.toContain('DTSTART:20260822T090000Z')
  })

  it('ends the event after the summed duration of the services', () => {
    const ics = buildAppointmentIcs(base)

    expect(ics).toContain('DTEND:20260822T095000')
  })

  it('names the services and the barber in the summary', () => {
    const ics = buildAppointmentIcs(base)

    expect(ics).toContain('SUMMARY:Corte + Barba com João Silva')
  })

  it('escapes RFC 5545 special characters instead of breaking the file', () => {
    const ics = buildAppointmentIcs({
      ...base,
      serviceNames: ['Corte, Barba; completo'],
    })

    expect(ics).toContain('SUMMARY:Corte\\, Barba\\; completo com João Silva')
  })

  it('carries the manage link so the client can find it again from the calendar', () => {
    const ics = buildAppointmentIcs({ ...base, manageUrl: 'https://x.test/agendamento/abc' })

    expect(ics).toContain('https://x.test/agendamento/abc')
  })

  it('produces a well-formed calendar with CRLF line endings', () => {
    const ics = buildAppointmentIcs(base)

    expect(ics.startsWith('BEGIN:VCALENDAR')).toBe(true)
    expect(ics.endsWith('END:VCALENDAR')).toBe(true)
    expect(ics).toContain('\r\n')
  })
})
