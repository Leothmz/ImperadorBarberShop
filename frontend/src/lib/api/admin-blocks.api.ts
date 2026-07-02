import apiClient from '@/lib/api/client'
import type { BarberBlockDto, CreateBarberBlockPayload } from './blocks.api'

export const adminBlocksApi = {
  getBlocks: (barberId: string): Promise<BarberBlockDto[]> =>
    apiClient.get(`/admin/barbers/${barberId}/blocks`).then(r => r.data),

  createBlock: (barberId: string, payload: CreateBarberBlockPayload): Promise<{ id: string }> =>
    apiClient.post(`/admin/barbers/${barberId}/blocks`, payload).then(r => r.data),

  deleteBlock: (barberId: string, blockId: string): Promise<void> =>
    apiClient.delete(`/admin/barbers/${barberId}/blocks/${blockId}`).then(() => undefined),
}
