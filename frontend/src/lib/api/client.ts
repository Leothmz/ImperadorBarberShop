import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { LoginResult } from '@/types/api.types'

const REFRESH_TOKEN_KEY = 'imperador_refresh_token'
const USER_ID_KEY = 'imperador_user_id'

// SECURITY NOTE — token storage strategy:
// - Access token: kept in-memory only (_accessToken below). Never written to
//   localStorage, sessionStorage, or a JS-readable cookie, so it is not
//   reachable by XSS payloads. It is intentionally lost on page refresh and
//   recovered via the refresh-token flow in AuthProvider on mount.
//
// - Refresh token: stored in localStorage so that sessions survive page
//   reloads. This does expose it to XSS. The ideal mitigation is to have the
//   backend set the refresh token in an HttpOnly cookie (not readable by JS),
//   which requires a coordinated backend change. Until that is implemented,
//   the surface is limited to the refresh token only — the short-lived access
//   token is never persisted to any storage accessible by JavaScript.
//
// - userId: stored alongside the refresh token in localStorage solely to
//   fulfil the /auth/refresh request body; it carries no privilege by itself.

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

// NEXT_PUBLIC_API_URL must be set to the API origin WITHOUT the path prefix,
// e.g. http://localhost:5000 (as documented in CLAUDE.md and .env.local).
// The /api/v1 version prefix is appended here so that the single env variable
// stays clean and consistent with the backend base URL convention.
const apiClient = axios.create({
  baseURL: `${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'}/api/v1`,
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
          `${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'}/api/v1/auth/refresh`,
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
