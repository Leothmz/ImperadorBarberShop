import apiClient from './client'
import type {
  LoginPayload,
  LoginResult,
  RegisterClientPayload,
  RegisterBarberPayload,
} from '@/types/api.types'

export const authApi = {
  login(payload: LoginPayload) {
    return apiClient.post<LoginResult>('/auth/login', payload)
  },

  registerClient(payload: RegisterClientPayload) {
    return apiClient.post<LoginResult>('/auth/register/client', payload)
  },

  registerBarber(payload: RegisterBarberPayload) {
    return apiClient.post<LoginResult>('/auth/register/barber', payload)
  },

  refresh(userId: string, refreshToken: string) {
    return apiClient.post<LoginResult>('/auth/refresh', { userId, refreshToken })
  },
}
