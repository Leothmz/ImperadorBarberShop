/**
 * Formats a duration in minutes into a readable string.
 * Examples: 50 → "50 min", 60 → "1h", 90 → "1h 30min"
 */
export function formatDuration(minutes: number): string {
  if (minutes <= 0) return '0 min'
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60

  if (hours === 0) return `${mins} min`
  if (mins === 0) return `${hours}h`
  return `${hours}h ${mins}min`
}
