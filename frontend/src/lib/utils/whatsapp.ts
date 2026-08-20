/**
 * Link de contato direto da barbearia. Só existe quando
 * NEXT_PUBLIC_WHATSAPP_NUMBER está configurado — sem número real configurado a
 * UI esconde o contato em vez de inventar um.
 */
export function shopWhatsappLink(message?: string): string | null {
  const digits = (process.env.NEXT_PUBLIC_WHATSAPP_NUMBER ?? '').replace(/\D/g, '')
  if (!digits) return null

  const query = message ? `?text=${encodeURIComponent(message)}` : ''
  return `https://wa.me/${digits}${query}`
}
