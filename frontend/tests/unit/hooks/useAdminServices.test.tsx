import { describe, it, expect, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import type { ReactNode } from 'react'
import { createTestQueryClient } from '../test-utils'
import { server } from '../../mocks/server'
import { useCreateService, useDeactivateService } from '@/hooks/useAdminServices'

const BASE_URL = 'http://localhost:5000/api/v1'

function setup() {
  const queryClient = createTestQueryClient()
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { queryClient, invalidate, wrapper }
}

function invalidatedKeys(invalidate: ReturnType<typeof vi.spyOn>) {
  return invalidate.mock.calls.map((call) => JSON.stringify(call[0]?.queryKey))
}

describe('admin service mutations', () => {
  it('refreshes the public catalog too, not just the admin list', async () => {
    server.use(
      http.post(`${BASE_URL}/services`, () => HttpResponse.json({ id: 'novo' }, { status: 201 }))
    )
    const { invalidate, wrapper } = setup()

    const { result } = renderHook(() => useCreateService(), { wrapper })
    result.current.mutate({
      name: 'Pezinho',
      description: 'Acabamento',
      price: 15,
      durationMinutes: 10,
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    // A home e o agendamento leem ['services']; invalidar só a lista do admin
    // deixava o próprio admin vendo a tabela antiga depois de cadastrar.
    const keys = invalidatedKeys(invalidate)
    expect(keys).toContain(JSON.stringify(['admin', 'services']))
    expect(keys).toContain(JSON.stringify(['services']))
  })

  it('does the same when a service is deactivated', async () => {
    server.use(
      http.patch(
        `${BASE_URL}/services/:id/deactivate`,
        () => new HttpResponse(null, { status: 204 })
      )
    )
    const { invalidate, wrapper } = setup()

    const { result } = renderHook(() => useDeactivateService(), { wrapper })
    result.current.mutate('svc-1')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(invalidatedKeys(invalidate)).toContain(JSON.stringify(['services']))
  })
})
