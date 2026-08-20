interface IcsInput {
  /** Horário de parede, sem fuso: "2026-08-22T09:00:00" — igual ao que a API devolve. */
  scheduledAt: string
  durationMinutes: number
  barberName: string
  serviceNames: string[]
  /** Link de gestão do agendamento, guardado na descrição do evento. */
  manageUrl?: string
}

/** Escapa vírgula, ponto e vírgula e quebra de linha, como manda o RFC 5545. */
function escapeText(value: string): string {
  return value.replace(/([\\,;])/g, '\\$1').replace(/\r?\n/g, '\\n')
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/** "2026-08-22T09:00:00" → "20260822T090000" (hora local flutuante, sem Z). */
function toIcsLocal(date: Date): string {
  return (
    `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}` +
    `T${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`
  )
}

/**
 * Gera o .ics do agendamento. O evento fica em horário local flutuante de
 * propósito: a API trabalha em horário de parede, e converter para UTC aqui
 * deslocaria o compromisso na agenda do cliente.
 */
export function buildAppointmentIcs({
  scheduledAt,
  durationMinutes,
  barberName,
  serviceNames,
  manageUrl,
}: IcsInput): string {
  const start = new Date(scheduledAt)
  const end = new Date(start.getTime() + durationMinutes * 60 * 1000)

  const summary = serviceNames.length
    ? `${serviceNames.join(' + ')} com ${barberName}`
    : `Agendamento com ${barberName}`

  const description = manageUrl
    ? `O Imperador Barber Shop\nGerencie ou cancele: ${manageUrl}`
    : 'O Imperador Barber Shop'

  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//O Imperador Barber Shop//Agendamento//PT-BR',
    'CALSCALE:GREGORIAN',
    'BEGIN:VEVENT',
    `UID:${toIcsLocal(start)}-${Math.random().toString(36).slice(2, 10)}@imperadorbarber`,
    `DTSTAMP:${toIcsLocal(new Date())}`,
    `DTSTART:${toIcsLocal(start)}`,
    `DTEND:${toIcsLocal(end)}`,
    `SUMMARY:${escapeText(summary)}`,
    `DESCRIPTION:${escapeText(description)}`,
    'BEGIN:VALARM',
    'TRIGGER:-PT2H',
    'ACTION:DISPLAY',
    `DESCRIPTION:${escapeText(summary)}`,
    'END:VALARM',
    'END:VEVENT',
    'END:VCALENDAR',
  ]

  return lines.join('\r\n')
}

/** Entrega o .ics ao navegador como download. */
export function downloadIcs(filename: string, content: string): void {
  const blob = new Blob([content], { type: 'text/calendar;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  document.body.removeChild(anchor)
  URL.revokeObjectURL(url)
}
