/**
 * Formats a date/time string for Brazilian locale.
 */

/**
 * Formats an ISO datetime string to Brazilian date format.
 * Example: "2024-03-15T14:30:00" → "15/03/2024"
 */
export function formatDate(isoString: string): string {
  const date = new Date(isoString)
  return date.toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

/**
 * Formats an ISO datetime string to Brazilian time format.
 * Example: "2024-03-15T14:30:00" → "14:30"
 */
export function formatTime(isoString: string): string {
  const date = new Date(isoString)
  return date.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  })
}

/**
 * Formats an ISO datetime to full Brazilian date+time.
 * Example: "2024-03-15T14:30:00" → "15/03/2024 às 14:30"
 */
export function formatDateTime(isoString: string): string {
  return `${formatDate(isoString)} às ${formatTime(isoString)}`
}

/**
 * Formats a price number as Brazilian currency.
 * Example: 49.9 → "R$ 49,90"
 */
export function formatCurrency(value: number): string {
  return value.toLocaleString('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  })
}

/**
 * Formats a "HH:mm:ss" time slot string to "HH:mm".
 * Example: "09:30:00" → "09:30"
 */
export function formatTimeSlot(slot: string): string {
  return slot.substring(0, 5)
}

/**
 * Formats a Date object to YYYY-MM-DD string for API calls.
 */
export function toApiDate(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}
