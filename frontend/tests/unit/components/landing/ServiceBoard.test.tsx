import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '../../test-utils'
import { createTestQueryClient } from '../../test-utils'
import { server } from '../../../mocks/server'
import { mockServices } from '../../../mocks/handlers'
import { ServiceBoard } from '@/components/landing/ServiceBoard'

const BASE_URL = 'http://localhost:5000/api/v1'

describe('ServiceBoard', () => {
  it('renders the catalog the API returns, not a hardcoded list', async () => {
    server.use(
      http.get(`${BASE_URL}/services`, () =>
        HttpResponse.json([
          {
            id: 'novo-1',
            name: 'Navalhado Premium',
            description: 'Acabamento na navalha',
            durationMinutes: 45,
            price: 70,
            isActive: true,
            photoUrl: null,
            addons: [],
          },
        ])
      )
    )

    render(<ServiceBoard />)

    await waitFor(() => {
      expect(screen.getByText('Navalhado Premium')).toBeInTheDocument()
    })
    expect(screen.getByText('R$ 70,00')).toBeInTheDocument()
    expect(screen.getByText('45 min')).toBeInTheDocument()
  })

  it('picks up a service the admin adds, without a reload', async () => {
    const queryClient = createTestQueryClient()

    render(
      <QueryClientProvider client={queryClient}>
        <ServiceBoard />
      </QueryClientProvider>
    )

    await waitFor(() => expect(screen.getByText('Corte Clássico')).toBeInTheDocument())
    expect(screen.queryByText('Pezinho')).not.toBeInTheDocument()

    // O admin cadastra um serviço novo…
    server.use(
      http.get(`${BASE_URL}/services`, () =>
        HttpResponse.json([
          ...mockServices,
          {
            id: 'novo-2',
            name: 'Pezinho',
            description: 'Acabamento',
            durationMinutes: 10,
            price: 15,
            isActive: true,
            photoUrl: null,
            addons: [],
          },
        ])
      )
    )

    // …e a tabela revalida sozinha (aqui forçamos o mesmo caminho que o foco da
    // aba e o intervalo disparam em produção).
    await queryClient.invalidateQueries({ queryKey: ['services'] })

    await waitFor(() => {
      expect(screen.getByText('Pezinho')).toBeInTheDocument()
    })
    expect(screen.getByText('R$ 15,00')).toBeInTheDocument()
  })

  it('drops a service the admin deactivated', async () => {
    server.use(
      http.get(`${BASE_URL}/services`, () =>
        HttpResponse.json([{ ...mockServices[0], isActive: false }])
      )
    )

    render(<ServiceBoard />)

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /o que fazemos/i })).toBeInTheDocument()
    })
    expect(screen.queryByText(mockServices[0].name)).not.toBeInTheDocument()
  })

  it('says the board is unavailable instead of showing an empty table', async () => {
    server.use(
      http.get(`${BASE_URL}/services`, () => new HttpResponse(null, { status: 500 }))
    )

    render(<ServiceBoard />)

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/não conseguimos carregar a tabela/i)
    })
  })
})
