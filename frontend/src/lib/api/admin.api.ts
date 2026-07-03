import apiClient from './client'
import type {
  AdminBarber,
  Appointment,
  FinancialSummary,
  FinancialByBarberItem,
  FinancialByServiceItem,
  Expense,
  FinancialTimelineItem,
  CreateExpensePayload,
  CreateBarberPayload,
  CreateServicePayload,
  UpdateServicePayload,
  PaymentMethod,
} from '@/types/api.types'

export const adminApi = {
  // Barbers
  getBarbers: () =>
    apiClient.get<AdminBarber[]>('/admin/barbers').then((r) => r.data),

  createBarber: (payload: CreateBarberPayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('email', payload.email)
    form.append('password', payload.password)
    payload.availability.forEach((a, i) => {
      form.append(`availability[${i}].dayOfWeek`, a.dayOfWeek)
      form.append(`availability[${i}].startTime`, a.startTime)
      form.append(`availability[${i}].endTime`, a.endTime)
    })
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.post<{ id: string }>('/admin/barbers', form, { headers: { 'Content-Type': undefined } }).then((r) => r.data)
  },

  deactivateBarber: (id: string) =>
    apiClient.patch(`/admin/barbers/${id}/deactivate`),

  activateBarber: (id: string) =>
    apiClient.patch(`/admin/barbers/${id}/activate`),

  // Password
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.patch('/admin/profile/password', { currentPassword, newPassword }),

  // Financial
  getSummary: (from: string, to: string) =>
    apiClient
      .get<FinancialSummary>('/admin/financial/summary', { params: { from, to } })
      .then((r) => r.data),

  getByBarber: (from: string, to: string) =>
    apiClient
      .get<FinancialByBarberItem[]>('/admin/financial/by-barber', { params: { from, to } })
      .then((r) => r.data),

  getByService: (from: string, to: string) =>
    apiClient
      .get<FinancialByServiceItem[]>('/admin/financial/by-service', { params: { from, to } })
      .then((r) => r.data),

  exportCsv: (from: string, to: string) =>
    apiClient
      .get<Blob>('/admin/financial/export', {
        params: { from, to },
        responseType: 'blob',
      })
      .then((r) => r.data),

  getBarberAppointments: (barberId: string) =>
    apiClient.get<Appointment[]>(`/admin/barbers/${barberId}/appointments`).then((r) => r.data),

  getExpenses: (from: string, to: string) =>
    apiClient.get<Expense[]>('/admin/financial/expenses', { params: { from, to } }).then((r) => r.data),

  createExpense: (payload: CreateExpensePayload) =>
    apiClient.post<{ id: string }>('/admin/financial/expenses', payload).then((r) => r.data),

  deleteExpense: (id: string) =>
    apiClient.delete(`/admin/financial/expenses/${id}`),

  getTimeline: (from: string, to: string, groupBy: 'day' | 'week' | 'month' = 'day') =>
    apiClient.get<FinancialTimelineItem[]>('/admin/financial/timeline', { params: { from, to, groupBy } })
      .then((r) => r.data),

  updateAppointmentPayment: (id: string, paymentMethod: PaymentMethod) =>
    apiClient.patch(`/admin/appointments/${id}/payment`, { paymentMethod }),
}

export const adminServicesApi = {
  createService: (payload: CreateServicePayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('description', payload.description)
    form.append('price', String(payload.price))
    form.append('durationMinutes', String(payload.durationMinutes))
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.post<{ id: string }>('/services', form, { headers: { 'Content-Type': undefined } }).then((r) => r.data)
  },

  updateService: (payload: UpdateServicePayload) => {
    const form = new FormData()
    form.append('name', payload.name)
    form.append('description', payload.description)
    form.append('price', String(payload.price))
    form.append('durationMinutes', String(payload.durationMinutes))
    if (payload.photo) form.append('photo', payload.photo)
    return apiClient.put(`/services/${payload.id}`, form, { headers: { 'Content-Type': undefined } })
  },

  deactivateService: (id: string) => apiClient.patch(`/services/${id}/deactivate`),
  activateService: (id: string) => apiClient.patch(`/services/${id}/activate`),

  addAddon: (serviceId: string, addonId: string) =>
    apiClient.post(`/services/${serviceId}/addons/${addonId}`),

  removeAddon: (serviceId: string, addonId: string) =>
    apiClient.delete(`/services/${serviceId}/addons/${addonId}`),
}
