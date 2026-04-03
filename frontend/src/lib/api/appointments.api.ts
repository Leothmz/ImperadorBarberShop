import apiClient from './client'
import type { Appointment, CreateAppointmentPayload } from '@/types/api.types'

export const appointmentsApi = {
  create(payload: CreateAppointmentPayload) {
    return apiClient.post<Appointment>('/appointments', payload)
  },

  getMine() {
    return apiClient.get<Appointment[]>('/appointments/mine')
  },

  cancel(id: string) {
    return apiClient.delete<void>(`/appointments/${id}`)
  },

  getBarberAppointments() {
    return apiClient.get<Appointment[]>('/appointments/barber')
  },

  accept(id: string) {
    return apiClient.patch<Appointment>(`/appointments/${id}/accept`)
  },

  reject(id: string) {
    return apiClient.patch<Appointment>(`/appointments/${id}/reject`)
  },

  complete(id: string) {
    return apiClient.patch<Appointment>(`/appointments/${id}/complete`)
  },
}
