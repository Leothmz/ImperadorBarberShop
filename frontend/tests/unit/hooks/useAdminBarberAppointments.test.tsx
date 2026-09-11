import { describe, it, expect, vi } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { type ReactNode } from 'react'
import { http, HttpResponse } from 'msw'
import { useAdminUpdateAppointmentPayment } from '@/hooks/useAdminBarberAppointments'
import { server } from '../../mocks/server'

describe('useAdminUpdateAppointmentPayment', () => {
  it('sends the plan payload and refreshes the list and the financial dashboard', async () => {
    const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const bodies: unknown[] = []
    server.use(
      http.patch('*/admin/appointments/:id/payment', async ({ request }) => {
        bodies.push(await request.json())
        return new HttpResponse(null, { status: 204 })
      })
    )
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    )
    const { result } = renderHook(() => useAdminUpdateAppointmentPayment('barber-1'), { wrapper })

    await act(() =>
      result.current.mutateAsync({
        id: 'appt-1',
        payment: { paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Pix', chargedAmount: 120 },
      })
    )

    expect(bodies).toEqual([{ paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Pix', chargedAmount: 120 }])
    // O plano entra no faturamento pelo valor cobrado: o dashboard precisa recarregar
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['admin', 'barber', 'appointments', 'barber-1'] })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['admin', 'financial'] })
  })
})
