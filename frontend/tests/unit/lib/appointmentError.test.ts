import { describe, it, expect } from 'vitest'
import { AxiosError, AxiosHeaders } from 'axios'
import { describeBookingError } from '@/lib/utils/appointmentError'

function axiosErrorWithStatus(status: number, data?: unknown): AxiosError {
  const error = new AxiosError('request failed')
  error.response = {
    status,
    statusText: '',
    data,
    headers: new AxiosHeaders(),
    config: { headers: new AxiosHeaders() },
  }
  return error
}

describe('describeBookingError', () => {
  it('sends a 409 back to the slot picker instead of telling the client to retry', () => {
    const result = describeBookingError(axiosErrorWithStatus(409))

    expect(result.action).toBe('pick-another-slot')
    expect(result.message).toMatch(/outra pessoa/i)
  })

  it('never tells a rate-limited client to try again', () => {
    const result = describeBookingError(axiosErrorWithStatus(429))

    expect(result.action).toBe('contact')
    // "tente novamente" num 429 garante o segundo erro — a mensagem tem que
    // mandar esperar, não repetir.
    expect(result.message).not.toMatch(/novamente/i)
    expect(result.message).toMatch(/aguarde/i)
  })

  it('surfaces the server message on a validation error', () => {
    const result = describeBookingError(
      axiosErrorWithStatus(400, { message: 'Telefone inválido.' })
    )

    expect(result.message).toBe('Telefone inválido.')
    expect(result.action).toBe('retry')
  })

  it('falls back to its own copy when a validation error carries no message', () => {
    const result = describeBookingError(axiosErrorWithStatus(422, {}))

    expect(result.message).toMatch(/nome e whatsapp/i)
  })

  it('distinguishes a dead connection from a server refusal', () => {
    const result = describeBookingError(new AxiosError('Network Error'))

    expect(result.message).toMatch(/conexão/i)
    expect(result.action).toBe('retry')
  })

  it('handles a non-axios throw', () => {
    const result = describeBookingError(new Error('boom'))

    expect(result.action).toBe('retry')
    expect(result.message).toMatch(/não foi possível/i)
  })

  it('treats a 500 as retryable', () => {
    expect(describeBookingError(axiosErrorWithStatus(500)).action).toBe('retry')
  })
})
