import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { LoginResult } from '@/types/api.types'

const USER_ID_KEY = 'imperador_user_id'
// Where the refresh token used to live, before the backend moved it into an
// HttpOnly cookie. Only ever removed now, so an old copy does not linger.
const LEGACY_REFRESH_TOKEN_KEY = 'imperador_refresh_token'

// SECURITY NOTE — token storage strategy:
// - Access token: kept in-memory only (_accessToken below). Never written to
//   localStorage, sessionStorage, or a JS-readable cookie, so it is not
//   reachable by XSS payloads. It is intentionally lost on page refresh and
//   recovered via the refresh-token flow in AuthProvider on mount.
//
// - Refresh token: never seen by JavaScript. The API sets it as an HttpOnly
//   cookie on /auth/login and /auth/refresh and leaves it out of the JSON body.
//   The auth calls go through this site's own origin (see authClient and the
//   rewrite in next.config.ts) so the cookie is stored where the Next middleware
//   can read it.
//
// - userId: stored in localStorage to fulfil the /auth/refresh request body, and
//   as the hint that a session may exist (JS cannot see the cookie itself). It
//   carries no privilege by itself.

// In-memory access token store (persists across hook calls, lost on page refresh)
let _accessToken: string | null = null

export function setAccessToken(token: string | null) {
  _accessToken = token
}

export function getAccessToken(): string | null {
  return _accessToken
}

export function getStoredUserId(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem(USER_ID_KEY)
}

export function storeUserId(userId: string) {
  if (typeof window === 'undefined') return
  localStorage.setItem(USER_ID_KEY, userId)
  localStorage.removeItem(LEGACY_REFRESH_TOKEN_KEY)
}

export function clearStoredAuth() {
  if (typeof window === 'undefined') return
  localStorage.removeItem(USER_ID_KEY)
  localStorage.removeItem(LEGACY_REFRESH_TOKEN_KEY)
}

// Session endpoints (login, refresh, logout) are called on this site's own origin
// and proxied to the API by the rewrite in next.config.ts: the HttpOnly refresh
// cookie the API sets then belongs to this host, where the middleware sees it,
// and same-origin requests carry it back without CORS credentials.
export const authClient = axios.create({
  baseURL: '/api/v1/auth',
  headers: {
    'Content-Type': 'application/json',
  },
})

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
      const userId = getStoredUserId()

      if (!userId) {
        // No session to refresh, propagate error
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
        // The refresh token rides in the HttpOnly cookie; the API rotates it in the response
        const response = await authClient.post<LoginResult>('/refresh', { userId })
        const { accessToken, userId: newUserId } = response.data

        setAccessToken(accessToken)
        storeUserId(newUserId)
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
