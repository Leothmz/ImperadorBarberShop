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
  storeRefreshData,
  clearStoredAuth,
  getStoredRefreshToken,
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

const ROLE_COOKIE = 'imperador_access_role'

function setCookie(name: string, value: string) {
  if (typeof document === 'undefined') return
  document.cookie = `${name}=${value};path=/;max-age=${60 * 60 * 24 * 7}`
}

function deleteCookie(name: string) {
  if (typeof document === 'undefined') return
  document.cookie = `${name}=;path=/;max-age=0`
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const login = useCallback((result: LoginResult) => {
    setAccessToken(result.accessToken)
    storeRefreshData(result.refreshToken, result.userId)
    setCookie(ROLE_COOKIE, result.role)
    setUser({
      userId: result.userId,
      role: result.role,
      barberId: result.barberId,
    })
  }, [])

  const logout = useCallback(() => {
    setAccessToken(null)
    clearStoredAuth()
    deleteCookie(ROLE_COOKIE)
    setUser(null)
  }, [])

  // On mount: attempt to restore session via refresh token
  useEffect(() => {
    const refreshToken = getStoredRefreshToken()
    const userId = getStoredUserId()

    if (!refreshToken || !userId) {
      setIsLoading(false)
      return
    }

    authApi
      .refresh(userId, refreshToken)
      .then((res) => {
        login(res.data)
      })
      .catch(() => {
        clearStoredAuth()
        deleteCookie(ROLE_COOKIE)
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
