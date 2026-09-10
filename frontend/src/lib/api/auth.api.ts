import apiClient, { authClient } from './client'
import type { LoginPayload, LoginResult, RegisterBarberPayload } from '@/types/api.types'

// Shape returned by the backend on HTTP 201 for registration endpoints.
export interface RegisterResult {
  id: string
}

export const authApi = {
  login(payload: LoginPayload) {
    return authClient.post<LoginResult>('/login', payload)
  },

  registerBarber(payload: RegisterBarberPayload) {
    return apiClient.post<RegisterResult>('/auth/register/barber', payload)
  },

  /** The refresh token itself travels in the HttpOnly cookie, never in JS. */
  refresh(userId: string) {
    return authClient.post<LoginResult>('/refresh', { userId })
  },

  /** Clears the HttpOnly refresh cookie, which page JavaScript cannot delete. */
  logout() {
    return authClient.post<void>('/logout')
  },
}
