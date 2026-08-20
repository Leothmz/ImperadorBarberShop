import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminServicesApi } from '@/lib/api/admin.api'
import type { CreateServicePayload, UpdateServicePayload } from '@/types/api.types'
import apiClient from '@/lib/api/client'
import type { Service } from '@/types/api.types'

function getAllServices() {
  return apiClient.get<Service[]>('/admin/services').then((r) => r.data)
}

/**
 * Toda alteração no catálogo tem que derrubar as duas caches: a do admin
 * (`['admin','services']`, que inclui inativos) e a pública (`['services']`, que
 * alimenta a home e o agendamento). Invalidar só a primeira deixava o próprio
 * admin vendo a tabela antiga no site depois de mexer nela.
 */
function invalidateServiceCaches(qc: ReturnType<typeof useQueryClient>) {
  qc.invalidateQueries({ queryKey: ['admin', 'services'] })
  qc.invalidateQueries({ queryKey: ['services'] })
}

export function useAdminAllServices() {
  return useQuery({ queryKey: ['admin', 'services'], queryFn: getAllServices })
}

export function useCreateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateServicePayload) => adminServicesApi.createService(payload),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useUpdateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateServicePayload) => adminServicesApi.updateService(payload),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useDeleteService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminServicesApi.deleteService(id),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useDeactivateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminServicesApi.deactivateService(id),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useActivateService() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminServicesApi.activateService(id),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useAddAddon() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ serviceId, addonId }: { serviceId: string; addonId: string }) =>
      adminServicesApi.addAddon(serviceId, addonId),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}

export function useRemoveAddon() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ serviceId, addonId }: { serviceId: string; addonId: string }) =>
      adminServicesApi.removeAddon(serviceId, addonId),
    onSuccess: () => invalidateServiceCaches(qc),
  })
}
