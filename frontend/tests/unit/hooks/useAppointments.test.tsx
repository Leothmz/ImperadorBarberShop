import { describe, it, expect } from 'vitest'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import {
  useBarberAppointments,
  useCreateAppointment,
  useAppointmentByToken,
  useCancelAppointmentByToken,
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

describe('useBarberAppointments', () => {
  it('returns barber appointments after loading', async () => {
    const { result } = renderHook(() => useBarberAppointments(), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toHaveLength(2)
  })
})

describe('useCreateAppointment', () => {
  it('creates an appointment and returns its access token', async () => {
    const { result } = renderHook(() => useCreateAppointment(), {
      wrapper: createWrapper(),
    })

    let data: Awaited<ReturnType<typeof result.current.mutateAsync>> | undefined

    await act(async () => {
      data = await result.current.mutateAsync({
        clientName: 'João',
        clientPhone: '+5511999990000',
        barberId: 'barber-1',
        scheduledAt: new Date().toISOString(),
        serviceIds: ['service-1'],
      })
    })

    expect(data?.id).toBeDefined()
    expect(data?.accessToken).toBeDefined()
  })
})

describe('useAppointmentByToken', () => {
  it('returns the managed appointment for a token', async () => {
    const { result } = renderHook(() => useAppointmentByToken('mock-access-token-1'), {
      wrapper: createWrapper(),
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.status).toBe('Accepted')
  })
})

describe('useCancelAppointmentByToken', () => {
  it('cancels the appointment for a token', async () => {
    const { result } = renderHook(() => useCancelAppointmentByToken('mock-access-token-1'), {
      wrapper: createWrapper(),
    })

    await act(async () => {
      await result.current.mutateAsync()
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
  })
})
