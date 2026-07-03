import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { CreateBarberPayload, UpdateBarberPayload } from '@/types/api.types'

export function useAdminBarbers() {
  return useQuery({ queryKey: ['admin', 'barbers'], queryFn: adminApi.getBarbers })
}

export function useCreateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateBarberPayload) => adminApi.createBarber(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useUpdateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateBarberPayload) => adminApi.updateBarber(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useDeleteBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteBarber(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useDeactivateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.deactivateBarber(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}

export function useActivateBarber() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.activateBarber(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'barbers'] }),
  })
}
