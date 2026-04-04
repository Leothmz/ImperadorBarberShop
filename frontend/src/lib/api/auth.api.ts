import apiClient from './client'
import type {
  LoginPayload,
  LoginResult,
  RegisterClientPayload,
  RegisterBarberPayload,
} from '@/types/api.types'

// Shape returned by the backend on HTTP 201 for registration endpoints.
export interface RegisterResult {
  id: string
}

export const authApi = {
  login(payload: LoginPayload) {
    return apiClient.post<LoginResult>('/auth/login', payload)
  },

  registerClient(payload: RegisterClientPayload) {
    return apiClient.post<RegisterResult>('/auth/register/client', payload)
  },

  registerBarber(payload: RegisterBarberPayload) {
    return apiClient.post<RegisterResult>('/auth/register/barber', payload)
  },

  refresh(userId: string, refreshToken: string) {
    return apiClient.post<LoginResult>('/auth/refresh', { userId, refreshToken })
  },
}
