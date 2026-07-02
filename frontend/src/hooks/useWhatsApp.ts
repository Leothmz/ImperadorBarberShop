import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { whatsappApi } from '@/lib/api/whatsapp.api'
import type { UpdateNotificationSettingsPayload } from '@/types/api.types'

export function useWhatsAppStatus(refetchInterval?: number) {
  return useQuery({
    queryKey: ['admin', 'whatsapp', 'status'],
    queryFn: whatsappApi.getStatus,
    refetchInterval: refetchInterval ?? 10_000,
  })
}

export function useWhatsAppQr() {
  return useQuery({
    queryKey: ['admin', 'whatsapp', 'qr'],
    queryFn: whatsappApi.getQr,
    refetchInterval: 5_000,
  })
}

export function useDisconnectWhatsApp() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: whatsappApi.disconnect,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'whatsapp', 'status'] }),
  })
}

export function useNotificationSettings() {
  return useQuery({
    queryKey: ['admin', 'notifications', 'settings'],
    queryFn: whatsappApi.getNotificationSettings,
  })
}

export function useUpdateNotificationSettings() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateNotificationSettingsPayload) =>
      whatsappApi.updateNotificationSettings(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'notifications', 'settings'] }),
  })
}
