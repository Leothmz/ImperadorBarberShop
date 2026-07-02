import apiClient from './client'

export interface BarberBlockDto {
  id: string
  startsAt: string
  endsAt: string
  description: string | null
  isRecurring: boolean
  recurrenceDays: number | null
  recurrenceEndsAt: string | null
  createdAt: string
}

export interface CreateBarberBlockPayload {
  startsAt: string
  endsAt: string
  description?: string
  isRecurring: boolean
  recurrenceDays?: number | null
  recurrenceEndsAt?: string | null
}

export const blocksApi = {
  getMyBlocks: (): Promise<BarberBlockDto[]> =>
    apiClient.get('/barbers/me/blocks').then(r => r.data),

  createBlock: (payload: CreateBarberBlockPayload): Promise<{ id: string }> =>
    apiClient.post('/barbers/me/blocks', payload).then(r => r.data),

  deleteBlock: (id: string): Promise<void> =>
    apiClient.delete(`/barbers/me/blocks/${id}`).then(() => undefined),
}
