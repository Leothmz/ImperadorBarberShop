import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import { useServices } from '@/hooks/useServices'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('useServices', () => {
  it('returns loading state initially', () => {
    const { result } = renderHook(() => useServices(), { wrapper: createWrapper() })
    expect(result.current.isLoading).toBe(true)
  })

  it('returns services data after loading', async () => {
    const { result } = renderHook(() => useServices(), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(4)
  })

  it('returns services including inactive ones', async () => {
    const { result } = renderHook(() => useServices(), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const inactive = result.current.data?.filter((s) => !s.isActive)
    expect(inactive).toHaveLength(1)
    expect(inactive?.[0].name).toBe('Hidratação')
  })
})
