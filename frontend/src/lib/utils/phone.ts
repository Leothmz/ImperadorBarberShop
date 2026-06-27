export function normalizeBrPhone(raw: string): string {
  const digits = raw.replace(/\D/g, '')
  const withoutCountryCode = digits.startsWith('55') ? digits.slice(2) : digits
  return `+55${withoutCountryCode}`
}

export function isValidBrPhone(value: string): boolean {
  return /^\+55\d{11}$/.test(value)
}
