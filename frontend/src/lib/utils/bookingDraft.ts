import type { Barber, Service } from '@/types/api.types'
import { toApiDate } from '@/lib/utils/formatDateTime'

const STORAGE_KEY = 'imperador_booking_draft'

export interface BookingDraft {
  step: number
  barber: Barber | null
  serviceIds: string[]
  /** "YYYY-MM-DD" — guardar a Date serializada em ISO deslocaria o dia por fuso. */
  date: string | null
  slot: string | null
  clientName: string
  clientPhone: string
  notes: string
}

export const emptyDraft: BookingDraft = {
  step: 1,
  barber: null,
  serviceIds: [],
  date: null,
  slot: null,
  clientName: '',
  clientPhone: '',
  notes: '',
}

/**
 * Passo mais avançado que os dados do rascunho realmente sustentam. Sem isso um
 * rascunho meio preenchido restaura direto no passo 4 e renderiza um card vazio.
 */
export function clampStep(draft: BookingDraft): number {
  if (!draft.barber) return 1
  if (draft.serviceIds.length === 0) return 2
  if (!draft.date || !draft.slot) return 3
  return Math.min(Math.max(draft.step, 1), 4)
}

/** "2026-08-22" → Date local. `new Date("2026-08-22")` seria interpretado como UTC. */
export function parseDraftDate(value: string | null): Date | null {
  if (!value) return null
  const [y, m, d] = value.split('-').map(Number)
  if (!y || !m || !d) return null
  return new Date(y, m - 1, d)
}

export function serializeDraftDate(date: Date | null): string | null {
  return date ? toApiDate(date) : null
}

export function loadDraft(): BookingDraft | null {
  if (typeof window === 'undefined') return null
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<BookingDraft>
    const draft: BookingDraft = { ...emptyDraft, ...parsed }
    return { ...draft, step: clampStep(draft) }
  } catch {
    // Rascunho corrompido ou storage bloqueado (aba anônima): começa do zero.
    return null
  }
}

export function saveDraft(draft: BookingDraft): void {
  if (typeof window === 'undefined') return
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(draft))
  } catch {
    // Persistir é conforto, não requisito: sem storage o fluxo segue em memória.
  }
}

export function clearDraft(): void {
  if (typeof window === 'undefined') return
  try {
    window.sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // idem
  }
}

/**
 * Remove do carrinho os serviços que sumiram do catálogo — o admin pode
 * desativar um serviço enquanto alguém está agendando, e o catálogo revalida
 * sozinho. Sem isto o resumo continuaria somando um item que a API vai recusar.
 *
 * Devolve o próprio array quando não há nada a remover: criar um array novo a
 * cada checagem colocaria o efeito que chama isto em laço infinito.
 */
export function pruneMissingServiceIds(
  selectedIds: string[],
  services: Service[] | undefined
): string[] {
  if (!services) return selectedIds

  const available = new Set<string>()
  for (const service of services) {
    available.add(service.id)
    for (const addon of service.addons ?? []) available.add(addon.id)
  }

  const kept = selectedIds.filter((id) => available.has(id))
  return kept.length === selectedIds.length ? selectedIds : kept
}
