import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import DashboardPage from '@/app/admin/dashboard/page'
import { http, HttpResponse } from 'msw'
import { server } from '../../../mocks/server'

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
  return Wrapper
}

beforeEach(() => {
  server.use(
    http.get('*/admin/financial/summary', () =>
      HttpResponse.json({
        totalRevenue: 250,
        totalAppointments: 5,
        averageTicket: 50,
        from: '2026-07-01',
        to: '2026-07-31',
      })
    ),
    http.get('*/admin/financial/by-barber', () => HttpResponse.json([])),
    http.get('*/admin/financial/by-service', () => HttpResponse.json([]))
  )
})

describe('DashboardPage', () => {
  it('renders summary cards', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    expect(await screen.findByText(/R\$\s*250/)).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('renders export CSV link', () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    expect(screen.getByRole('link', { name: /exportar csv/i })).toBeInTheDocument()
  })

  it('renders financial section headings', () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    expect(screen.getByText('Dashboard Financeiro')).toBeInTheDocument()
    expect(screen.getByText('Por Barbeiro')).toBeInTheDocument()
    expect(screen.getByText('Por Serviço')).toBeInTheDocument()
  })

  it('renders period preset buttons', () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    expect(screen.getByRole('button', { name: 'Hoje' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Esta semana' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Este mês' })).toBeInTheDocument()
  })
})
