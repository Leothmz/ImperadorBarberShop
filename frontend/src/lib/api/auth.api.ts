import { authClient } from './client'
import type { LoginPayload, LoginResult } from '@/types/api.types'

export const authApi = {
  login(payload: LoginPayload) {
    return authClient.post<LoginResult>('/login', payload)
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
