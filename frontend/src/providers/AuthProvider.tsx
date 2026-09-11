'use client'

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from 'react'
import type { UserRole, LoginResult } from '@/types/api.types'
import {
  setAccessToken,
  storeUserId,
  clearStoredAuth,
  getStoredUserId,
} from '@/lib/api/client'
import { authApi } from '@/lib/api/auth.api'

interface AuthUser {
  userId: string
  role: UserRole
  barberId: string | null
}

interface AuthContextValue {
  user: AuthUser | null
  isLoading: boolean
  login: (result: LoginResult) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

interface AuthProviderProps {
  children: ReactNode
}

// SECURITY NOTE — route guard:
// There is no client-written cookie any more. The API sets the refresh token as
// an HttpOnly cookie on login/refresh; the Next.js middleware only lets /admin and
// /barber render when that cookie is present, and the area layouts then check the
// role this provider holds — which only ever comes from an API response.

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const login = useCallback((result: LoginResult) => {
    setAccessToken(result.accessToken)
    storeUserId(result.userId)
    setUser({
      userId: result.userId,
      role: result.role,
      barberId: result.barberId,
    })
  }, [])

  const logout = useCallback(() => {
    setAccessToken(null)
    clearStoredAuth()
    setUser(null)
    // The HttpOnly cookie can only be cleared by the API. Best effort: if this
    // fails, the stored userId is already gone, so the session is not restored.
    authApi.logout().catch(() => {})
  }, [])

  // On mount: attempt to restore the session from the HttpOnly refresh cookie
  useEffect(() => {
    const userId = getStoredUserId()

    // JS cannot see the cookie; without the stored userId there is no session to
    // restore, and anonymous visitors do not fire a refresh that can only fail
    if (!userId) {
      setIsLoading(false)
      return
    }

    authApi
      .refresh(userId)
      .then((res) => {
        login(res.data)
      })
      .catch(() => {
        clearStoredAuth()
      })
      .finally(() => {
        setIsLoading(false)
      })
  }, [login])

  // Listen for auth:logout events dispatched by the axios interceptor
  useEffect(() => {
    const handleLogout = () => logout()
    window.addEventListener('auth:logout', handleLogout)
    return () => window.removeEventListener('auth:logout', handleLogout)
  }, [logout])

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuthContext must be used within AuthProvider')
  }
  return ctx
}
