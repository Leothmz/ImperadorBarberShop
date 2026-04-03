import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import { useBarbers, useBarber, useBarberReviews } from '@/hooks/useBarbers'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('useBarbers', () => {
  it('returns loading state initially', () => {
    const { result } = renderHook(() => useBarbers(), { wrapper: createWrapper() })
    expect(result.current.isLoading).toBe(true)
  })

  it('returns barbers data after loading', async () => {
    const { result } = renderHook(() => useBarbers(), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
    expect(result.current.data?.[0].name).toBe('Carlos Andrade')
  })
})

describe('useBarber', () => {
  it('returns a single barber by id', async () => {
    const { result } = renderHook(() => useBarber('barber-1'), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.name).toBe('Carlos Andrade')
  })

  it('does not fetch when id is empty', () => {
    const { result } = renderHook(() => useBarber(''), { wrapper: createWrapper() })
    expect(result.current.fetchStatus).toBe('idle')
  })
})

describe('useBarberReviews', () => {
  it('returns reviews for a barber', async () => {
    const { result } = renderHook(() => useBarberReviews('barber-1'), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
    expect(result.current.data?.[0].clientName).toBe('João Silva')
  })
})
