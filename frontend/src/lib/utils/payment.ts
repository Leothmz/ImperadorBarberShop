import { formatCurrency } from '@/lib/utils/formatDateTime'
import type { Appointment, PaymentMethod, PlanKind, PlanTender } from '@/types/api.types'

/** Formas em que um plano é pago. Um "Cartão" só: a barbearia não separa crédito de débito. */
export const PLAN_TENDERS: PlanTender[] = ['Pix', 'Dinheiro', 'Cartão']

export const PAYMENT_ICONS: Record<PaymentMethod, string> = {
  Dinheiro: '💵',
  Cartão: '💳',
  Pix: '⚡',
  Plano: '📋',
}

export const PLAN_KIND_LABELS: Record<PlanKind, string> = {
  Pagamento: 'Pagamento',
  Recorrencia: 'Recorrência',
}

/**
 * O pagamento registrado, para exibir: "Pix", "Plano · Pagamento · Cartão",
 * "Plano · Recorrência". Nulo quando ainda não há pagamento.
 */
export function describePayment(
  appointment: Pick<Appointment, 'paymentMethod' | 'planKind' | 'planTender'>
): string | null {
  const { paymentMethod, planKind, planTender } = appointment
  if (!paymentMethod) return null
  if (paymentMethod !== 'Plano' || !planKind) return paymentMethod
  return ['Plano', PLAN_KIND_LABELS[planKind], planTender].filter(Boolean).join(' · ')
}

/**
 * O que o plano rendeu nesta visita, ao lado dos serviços (que continuam listados):
 * "R$ 120,00 cobrados no plano" ou "Coberto pelo plano: R$ 0,00 nesta visita". Nulo fora do plano.
 */
export function describePlanAmount(
  appointment: Pick<Appointment, 'paymentMethod' | 'planKind' | 'chargedAmount'>
): string | null {
  const { paymentMethod, planKind, chargedAmount } = appointment
  if (paymentMethod !== 'Plano' || !planKind) return null
  if (planKind === 'Recorrencia') return `Coberto pelo plano: ${formatCurrency(0)} nesta visita`
  return `${formatCurrency(chargedAmount ?? 0)} cobrados no plano`
}

export type ChargedAmountResult =
  | { amount: number; error: null }
  | { amount: null; error: string | null }

/**
 * Lê o valor digitado no pagamento do plano, com vírgula ou ponto nos centavos ("49,90",
 * "49.90", "120"). Zero vale; negativo e mais de dois centavos não, as mesmas regras do
 * servidor. Campo vazio não é erro ainda, só não pode ser enviado.
 */
export function parseChargedAmount(raw: string): ChargedAmountResult {
  const text = raw.trim().replace(/^R\$\s*/i, '')
  if (text === '') return { amount: null, error: null }
  if (text.startsWith('-')) return { amount: null, error: 'O valor não pode ser negativo.' }

  const match = /^(\d{1,7})(?:[.,](\d+))?$/.exec(text)
  if (!match) return { amount: null, error: 'Digite o valor em reais, como 120 ou 49,90.' }
  if ((match[2] ?? '').length > 2) return { amount: null, error: 'Use no máximo duas casas decimais.' }

  return { amount: Number(`${match[1]}.${match[2] ?? '0'}`), error: null }
}
