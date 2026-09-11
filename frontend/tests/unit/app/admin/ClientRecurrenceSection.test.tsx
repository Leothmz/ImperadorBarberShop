import { describe, it, expect } from 'vitest'
import { http, HttpResponse, delay } from 'msw'
import userEvent from '@testing-library/user-event'
import { render, screen, waitFor, within } from '../../test-utils'
import { ClientRecurrenceSection } from '@/app/admin/dashboard/ClientRecurrenceSection'
import type { ReinviteCandidate } from '@/types/api.types'
import { server } from '../../../mocks/server'

const joao: ReinviteCandidate = {
  clientId: 'client-joao',
  name: 'João Silva',
  phone: '+5511999990000',
  lastVisitAt: '2026-08-14T10:00:00',
  daysSinceLastVisit: 28,
  visitCount: 4,
}

const maria: ReinviteCandidate = {
  clientId: 'client-maria',
  name: 'Maria Souza',
  phone: '+5521988887777',
  lastVisitAt: '2026-08-17T15:30:00',
  daysSinceLastVisit: 25,
  visitCount: 1,
}

function candidates(list: ReinviteCandidate[]) {
  server.use(http.get('*/admin/clients/reinvite-candidates', () => HttpResponse.json(list)))
}

describe('ClientRecurrenceSection', () => {
  it('shows a loading state while the list is on its way', async () => {
    server.use(
      http.get('*/admin/clients/reinvite-candidates', async () => {
        await delay(50)
        return HttpResponse.json([])
      })
    )

    render(<ClientRecurrenceSection />)

    expect(screen.getByText('Carregando clientes…')).toBeInTheDocument()
    expect(await screen.findByText(/Ninguém sumindo agora/)).toBeInTheDocument()
  })

  it('explains the empty list instead of showing a blank card', async () => {
    candidates([])

    render(<ClientRecurrenceSection />)

    expect(
      await screen.findByText('Ninguém sumindo agora. Quem completar 25 dias sem voltar aparece aqui.')
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Convidar no WhatsApp' })).not.toBeInTheDocument()
  })

  it('lists each client with phone, last visit, days away and visit count', async () => {
    candidates([joao, maria])

    render(<ClientRecurrenceSection />)

    const row = (await screen.findByText('João Silva')).closest('li')!
    expect(within(row).getByText(/\(11\) 99999-0000/)).toBeInTheDocument()
    expect(within(row).getByText(/última visita 14\/08\/2026/)).toBeInTheDocument()
    expect(within(row).getByText(/4 visitas/)).toBeInTheDocument()
    expect(within(row).getByText('há 28 dias')).toBeInTheDocument()

    const mariaRow = screen.getByText('Maria Souza').closest('li')!
    expect(within(mariaRow).getByText(/1 visita$/)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Convidar no WhatsApp' })).toHaveLength(2)
  })

  it('invites the chosen client and refreshes the list', async () => {
    let list = [joao, maria]
    const invitedIds: string[] = []
    server.use(
      http.get('*/admin/clients/reinvite-candidates', () => HttpResponse.json(list)),
      http.post('*/admin/clients/:id/reinvite', ({ params }) => {
        invitedIds.push(params.id as string)
        list = [maria] // convidado sai da lista por 10 dias
        return new HttpResponse(null, { status: 204 })
      })
    )
    const user = userEvent.setup()

    render(<ClientRecurrenceSection />)

    const row = (await screen.findByText('João Silva')).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Convidar no WhatsApp' }))

    expect(await screen.findByText('Convite enviado para João Silva.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('João Silva')).not.toBeInTheDocument())
    expect(screen.getByText('Maria Souza')).toBeInTheDocument()
    expect(invitedIds).toEqual(['client-joao'])
  })

  it('shows why the invite was refused', async () => {
    candidates([joao])
    server.use(
      http.post('*/admin/clients/:id/reinvite', () =>
        HttpResponse.json(
          { status: 422, detail: 'O envio por WhatsApp está desativado. Ative o canal em WhatsApp → Notificações para convidar clientes.' },
          { status: 422 }
        )
      )
    )
    const user = userEvent.setup()

    render(<ClientRecurrenceSection />)

    await user.click(await screen.findByRole('button', { name: 'Convidar no WhatsApp' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('O envio por WhatsApp está desativado')
    expect(screen.queryByText(/Convite enviado/)).not.toBeInTheDocument()
  })

  it('says so when the list cannot be loaded', async () => {
    server.use(
      http.get('*/admin/clients/reinvite-candidates', () => new HttpResponse(null, { status: 500 }))
    )

    render(<ClientRecurrenceSection />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar os clientes.')
  })
})
