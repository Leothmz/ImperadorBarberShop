import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { blocksApi, CreateBarberBlockPayload } from '@/lib/api/blocks.api'

const BLOCKS_KEY = ['barber', 'blocks'] as const

export function useBarberBlocks() {
  return useQuery({ queryKey: BLOCKS_KEY, queryFn: blocksApi.getMyBlocks })
}

export function useCreateBarberBlock() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberBlockPayload) => blocksApi.createBlock(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: BLOCKS_KEY }),
  })
}

export function useDeleteBarberBlock() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => blocksApi.deleteBlock(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: BLOCKS_KEY }),
  })
}
