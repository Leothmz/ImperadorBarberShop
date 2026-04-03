import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { LoginResult } from '@/types/api.types'

const REFRESH_TOKEN_KEY = 'imperador_refresh_token'
const USER_ID_KEY = 'imperador_user_id'

// In-memory access token store (persists across hook calls, lost on page refresh)
let _accessToken: string | null = null

export function setAccessToken(token: string | null) {
  _accessToken = token
}

export function getAccessToken(): string | null {
  return _accessToken
}

export function getStoredRefreshToken(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

export function getStoredUserId(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(USER_ID_KEY)
}

export function storeRefreshData(refreshToken: string, userId: string) {
  if (typeof window === 'undefined') return
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken)
  localStorage.setItem(USER_ID_KEY, userId)
}

export function clearStoredAuth() {
  if (typeof window === 'undefined') return
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(USER_ID_KEY)
}

const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor: attach Bearer token
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = _accessToken
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Track if we are already refreshing to prevent infinite loops
let isRefreshing = false
let refreshSubscribers: Array<(token: string) => void> = []

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb)
}

function onTokenRefreshed(token: string) {
  refreshSubscribers.forEach((cb) => cb(token))
  refreshSubscribers = []
}

// Response interceptor: handle 401 — auto-refresh and retry
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean }

    if (error.response?.status === 401 && !originalRequest._retry) {
      const refreshToken = getStoredRefreshToken()
      const userId = getStoredUserId()

      if (!refreshToken || !userId) {
        // No refresh credentials, propagate error
        return Promise.reject(error)
      }

      if (isRefreshing) {
        // Queue the request until token is refreshed
        return new Promise((resolve) => {
          subscribeTokenRefresh((token: string) => {
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${token}`
            }
            resolve(apiClient(originalRequest))
          })
        })
      }

      originalRequest._retry = true
      isRefreshing = true

      try {
        const response = await axios.post<LoginResult>(
          `${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api/v1'}/auth/refresh`,
          { userId, refreshToken }
        )
        const { accessToken, refreshToken: newRefreshToken, userId: newUserId } = response.data

        setAccessToken(accessToken)
        storeRefreshData(newRefreshToken, newUserId)
        onTokenRefreshed(accessToken)

        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${accessToken}`
        }

        return apiClient(originalRequest)
      } catch {
        clearStoredAuth()
        setAccessToken(null)
        // Dispatch a custom event so AuthProvider can react
        if (typeof window !== 'undefined') {
          window.dispatchEvent(new CustomEvent('auth:logout'))
        }
        return Promise.reject(error)
      } finally {
        isRefreshing = false
      }
    }

    return Promise.reject(error)
  }
)

export default apiClient
