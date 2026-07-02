import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminBlocksApi } from '@/lib/api/admin-blocks.api'
import type { CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const blocksKey = (barberId: string) => ['admin', 'barbers', barberId, 'blocks'] as const

export function useAdminBarberBlocks(barberId: string) {
  return useQuery({
    queryKey: blocksKey(barberId),
    queryFn: () => adminBlocksApi.getBlocks(barberId),
  })
}

export function useAdminCreateBarberBlock(barberId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberBlockPayload) => adminBlocksApi.createBlock(barberId, payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(barberId) }),
  })
}

export function useAdminDeleteBarberBlock(barberId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (blockId: string) => adminBlocksApi.deleteBlock(barberId, blockId),
    onSuccess: () => qc.invalidateQueries({ queryKey: blocksKey(barberId) }),
  })
}
