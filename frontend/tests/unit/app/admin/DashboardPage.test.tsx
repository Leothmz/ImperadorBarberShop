import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
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
        totalExpenses: 100,
        netRevenue: 150,
      })
    ),
    http.get('*/admin/financial/by-barber', () => HttpResponse.json([])),
    http.get('*/admin/financial/by-service', () => HttpResponse.json([])),
    http.get('*/admin/financial/timeline', () => HttpResponse.json([])),
    http.get('*/admin/financial/expenses', () =>
      HttpResponse.json([
        { id: 'expense-1', amount: 100, description: 'Produto', date: '2026-07-01', createdAt: new Date().toISOString() },
      ])
    )
  )
})

describe('DashboardPage', () => {
  it('renders summary cards', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    expect(await screen.findByText(/R\$\s*250/)).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('renders export CSV button', () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    const btn = screen.getByRole('button', { name: /exportar csv/i })
    expect(btn).toBeInTheDocument()
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

  it('renders summary cards including Despesas and Lucro Líquido', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    await waitFor(() => {
      expect(screen.getAllByText('Despesas').length).toBeGreaterThanOrEqual(1)
      expect(screen.getByText('Lucro Líquido')).toBeInTheDocument()
      expect(screen.getByText('Receita Total')).toBeInTheDocument()
    })
  })

  it('renders groupBy buttons for timeline', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Dia' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Semana' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Mês' })).toBeInTheDocument()
    })
  })

  it('renders expenses section with form', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    await waitFor(() => {
      expect(screen.getAllByText('Despesas').length).toBeGreaterThanOrEqual(1)
      expect(screen.getByRole('button', { name: /Adicionar/i })).toBeInTheDocument()
    })
  })

  it('shows existing expenses from mock data', async () => {
    render(<DashboardPage />, { wrapper: createWrapper() })
    await waitFor(() => {
      expect(screen.getByText('Produto')).toBeInTheDocument()
    })
  })
})
