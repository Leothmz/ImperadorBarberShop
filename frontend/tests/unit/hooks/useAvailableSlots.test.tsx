import { describe, it, expect } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import { useAvailableSlots } from '@/hooks/useAvailableSlots'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('useAvailableSlots', () => {
  it('does not fetch when barberId is empty', () => {
    const { result } = renderHook(
      () => useAvailableSlots({ barberId: '', date: '2024-06-15', serviceIds: ['s1'] }),
      { wrapper: createWrapper() }
    )
    expect(result.current.fetchStatus).toBe('idle')
  })

  it('does not fetch when date is empty', () => {
    const { result } = renderHook(
      () => useAvailableSlots({ barberId: 'barber-1', date: '', serviceIds: ['s1'] }),
      { wrapper: createWrapper() }
    )
    expect(result.current.fetchStatus).toBe('idle')
  })

  it('does not fetch when serviceIds is empty', () => {
    const { result } = renderHook(
      () => useAvailableSlots({ barberId: 'barber-1', date: '2024-06-15', serviceIds: [] }),
      { wrapper: createWrapper() }
    )
    expect(result.current.fetchStatus).toBe('idle')
  })

  it('returns time slots when all params are provided', async () => {
    const { result } = renderHook(
      () =>
        useAvailableSlots({
          barberId: 'barber-1',
          date: '2024-06-15',
          serviceIds: ['service-1'],
        }),
      { wrapper: createWrapper() }
    )

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toContain('09:00:00')
    expect(result.current.data?.length).toBeGreaterThan(0)
  })
})
