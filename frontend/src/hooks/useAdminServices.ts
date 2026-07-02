import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminServicesApi } from '@/lib/api/admin.api'
import type { CreateServicePayload, UpdateServicePayload } from '@/types/api.types'
import apiClient from '@/lib/api/client'
import type { Service } from '@/types/api.types'

// Fetches ALL services (including inactive) for admin use
function getAllServices() {
  return apiClient.get<Service[]>('/services', { params: { includeInactive: true } }).then((r) => r.data)
}

export function useAdminAllServices() {
  return useQuery({ queryKey: ['admin', 'services'], queryFn: getAllServices })
}

export function useCreateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateServicePayload) => adminServicesApi.createService(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}

export function useUpdateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateServicePayload) => adminServicesApi.updateService(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}

export function useDeactivateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminServicesApi.deactivateService(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}

export function useActivateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminServicesApi.activateService(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}

export function useAddAddon() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ serviceId, addonId }: { serviceId: string; addonId: string }) =>
      adminServicesApi.addAddon(serviceId, addonId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}

export function useRemoveAddon() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ serviceId, addonId }: { serviceId: string; addonId: string }) =>
      adminServicesApi.removeAddon(serviceId, addonId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'services'] }),
  })
}
