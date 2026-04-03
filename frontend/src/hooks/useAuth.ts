import { useAuthContext } from '@/providers/AuthProvider'

/**
 * Convenience hook for consuming the auth context.
 * Re-exports the context value so callers don't need to import AuthProvider directly.
 */
export function useAuth() {
  return useAuthContext()
}
