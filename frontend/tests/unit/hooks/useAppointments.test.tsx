import { describe, it, expect } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import {
  useClientAppointments,
  useBarberAppointments,
  useCreateAppointment,
} from '@/hooks/useAppointments'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  return Wrapper
}

describe('useClientAppointments', () => {
  it('returns loading state initially', () => {
    const { result } = renderHook(() => useClientAppointments(), {
      wrapper: createWrapper(),
    })
    expect(result.current.isLoading).toBe(true)
  })

  it('returns client appointments after loading', async () => {
    const { result } = renderHook(() => useClientAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
  })

  it('includes appointments with correct statuses', async () => {
    const { result } = renderHook(() => useClientAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const statuses = result.current.data?.map((a) => a.status)
    expect(statuses).toContain('Accepted')
    expect(statuses).toContain('Completed')
  })
})

describe('useBarberAppointments', () => {
  it('returns barber appointments after loading', async () => {
    const { result } = renderHook(() => useBarberAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
  })

  it('includes pending appointments', async () => {
    const { result } = renderHook(() => useBarberAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const pending = result.current.data?.filter((a) => a.status === 'Pending')
    expect(pending).toHaveLength(1)
  })
})

describe('useCreateAppointment', () => {
  it('creates an appointment successfully', async () => {
    const { result } = renderHook(() => useCreateAppointment(), {
      wrapper: createWrapper(),
    })

    let data: Awaited<ReturnType<typeof result.current.mutateAsync>> | undefined

    await act(async () => {
      data = await result.current.mutateAsync({
        barberId: 'barber-1',
        scheduledAt: new Date().toISOString(),
        serviceIds: ['service-1'],
      })
    })

    // The returned data from mutateAsync is the created appointment
    expect(data).toBeDefined()
    expect(data?.barberId).toBe('barber-1')
    expect(data?.status).toBe('Pending')
  })
})
