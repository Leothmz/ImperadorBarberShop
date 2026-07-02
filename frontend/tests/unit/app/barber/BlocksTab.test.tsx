import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'

// We'll test a standalone BlocksTab component (to be created)
import BlocksTab from '@/app/barber/dashboard/BlocksTab'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => {
  server.use(
    http.get('*/barbers/me/blocks', () =>
      HttpResponse.json([
        {
          id: 'block-1',
          startsAt: '2026-08-01T12:00:00Z',
          endsAt: '2026-08-01T13:00:00Z',
          description: 'Almoço',
          isRecurring: false,
          recurrenceDays: null,
          recurrenceEndsAt: null,
          createdAt: '2026-07-01T00:00:00Z',
        },
      ])
    ),
    http.delete('*/barbers/me/blocks/*', () => new HttpResponse(null, { status: 204 }))
  )
})

describe('BlocksTab', () => {
  it('renders existing blocks', async () => {
    render(<BlocksTab />, { wrapper })
    expect(await screen.findByText('Almoço')).toBeInTheDocument()
  })

  it('renders add block button', () => {
    render(<BlocksTab />, { wrapper })
    expect(screen.getByRole('button', { name: /adicionar bloqueio/i })).toBeInTheDocument()
  })

  it('opens modal on add click', () => {
    render(<BlocksTab />, { wrapper })
    fireEvent.click(screen.getByRole('button', { name: /adicionar bloqueio/i }))
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
