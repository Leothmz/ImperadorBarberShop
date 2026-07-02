import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'
import AdminBlocksSection from '@/app/admin/barbers/AdminBlocksSection'

const BARBER_ID = 'barber-abc'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => {
  server.use(
    http.get(`*/admin/barbers/${BARBER_ID}/blocks`, () =>
      HttpResponse.json([
        {
          id: 'blk-1',
          startsAt: '2026-08-05T12:00:00Z',
          endsAt: '2026-08-05T13:00:00Z',
          description: 'Folga',
          isRecurring: false,
          recurrenceDays: null,
          recurrenceEndsAt: null,
          createdAt: '2026-07-01T00:00:00Z',
        },
      ])
    )
  )
})

describe('AdminBlocksSection', () => {
  it('renders block list for barber', async () => {
    render(<AdminBlocksSection barberId={BARBER_ID} />, { wrapper })
    expect(await screen.findByText('Folga')).toBeInTheDocument()
  })

  it('renders add block button', () => {
    render(<AdminBlocksSection barberId={BARBER_ID} />, { wrapper })
    expect(screen.getByRole('button', { name: /adicionar bloqueio/i })).toBeInTheDocument()
  })
})
