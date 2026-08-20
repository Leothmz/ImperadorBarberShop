import { AxiosError } from 'axios'

/**
 * Recuperação possível para cada falha. A UI escolhe o botão a partir daqui —
 * é o que impede um 429 de virar "tente novamente" (que garante o segundo erro)
 * e um 409 de jogar o cliente num laço tentando o mesmo horário.
 */
export type BookingErrorAction = 'pick-another-slot' | 'contact' | 'retry'

export interface BookingError {
  message: string
  action: BookingErrorAction
}

const SLOT_TAKEN: BookingError = {
  message:
    'Esse horário acabou de ser reservado por outra pessoa. Escolha outro horário para continuar.',
  action: 'pick-another-slot',
}

const RATE_LIMITED: BookingError = {
  message:
    'Você já fez vários agendamentos na última hora. Aguarde um pouco ou fale direto com a barbearia.',
  action: 'contact',
}

const NETWORK: BookingError = {
  message: 'Não conseguimos falar com a barbearia. Verifique sua conexão e tente de novo.',
  action: 'retry',
}

const UNKNOWN: BookingError = {
  message: 'Não foi possível criar o agendamento. Tente novamente.',
  action: 'retry',
}

/** Mensagem do backend, quando ela é apresentável para o cliente. */
function serverMessage(data: unknown): string | null {
  if (typeof data === 'string' && data.trim()) return data.trim()
  if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>
    for (const key of ['message', 'detail', 'title', 'error']) {
      const value = record[key]
      if (typeof value === 'string' && value.trim()) return value.trim()
    }
  }
  return null
}

export function describeBookingError(error: unknown): BookingError {
  if (!(error instanceof AxiosError)) return UNKNOWN

  const status = error.response?.status
  if (status === undefined) return NETWORK
  if (status === 409) return SLOT_TAKEN
  if (status === 429) return RATE_LIMITED

  if (status === 400 || status === 422) {
    return {
      message:
        serverMessage(error.response?.data) ??
        'Alguns dados do agendamento não foram aceitos. Confira seu nome e WhatsApp.',
      action: 'retry',
    }
  }

  return UNKNOWN
}
