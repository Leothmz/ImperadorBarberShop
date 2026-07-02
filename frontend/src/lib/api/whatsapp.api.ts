import apiClient from './client'
import type {
  WhatsAppQr,
  WhatsAppStatus,
  NotificationSettings,
  UpdateNotificationSettingsPayload,
} from '@/types/api.types'

export const whatsappApi = {
  getStatus: () =>
    apiClient.get<WhatsAppStatus>('/admin/whatsapp/status').then((r) => r.data),

  getQr: () =>
    apiClient.get<WhatsAppQr>('/admin/whatsapp/qr').then((r) => r.data),

  disconnect: () =>
    apiClient.post('/admin/whatsapp/disconnect'),

  getNotificationSettings: () =>
    apiClient.get<NotificationSettings>('/admin/notifications/settings').then((r) => r.data),

  updateNotificationSettings: (payload: UpdateNotificationSettingsPayload) =>
    apiClient.put('/admin/notifications/settings', payload),
}
