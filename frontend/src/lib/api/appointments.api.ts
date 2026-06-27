import apiClient from './client'
import type {
  Appointment,
  AppointmentManage,
  CreateAppointmentPayload,
  CreateAppointmentResult,
  CreateReviewByTokenPayload,
  CreateReviewByTokenResult,
} from '@/types/api.types'

export const appointmentsApi = {
  create(payload: CreateAppointmentPayload) {
    return apiClient.post<CreateAppointmentResult>('/appointments', payload)
  },

  getByToken(token: string) {
    return apiClient.get<AppointmentManage>(`/appointments/manage/${token}`)
  },

  cancelByToken(token: string) {
    return apiClient.post<void>(`/appointments/manage/${token}/cancel`)
  },

  reviewByToken(token: string, payload: CreateReviewByTokenPayload) {
    return apiClient.post<CreateReviewByTokenResult>(`/appointments/manage/${token}/review`, payload)
  },

  getBarberAppointments() {
    return apiClient.get<Appointment[]>('/appointments/barber')
  },

  cancelByBarber(id: string) {
    return apiClient.patch<void>(`/appointments/${id}/cancel-by-barber`)
  },

  complete(id: string) {
    return apiClient.patch<Appointment>(`/appointments/${id}/complete`)
  },
}
