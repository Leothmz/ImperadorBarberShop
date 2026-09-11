import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import userEvent from '@testing-library/user-event'
import { render, screen, waitFor, fireEvent, within } from '../../test-utils'
import AdminAppointmentsSection from '@/app/admin/barbers/AdminAppointmentsSection'
import type { Appointment } from '@/types/api.types'
import { mockBarberAppointments } from '../../../mocks/handlers'
import { server } from '../../../mocks/server'

/** Guarda o corpo de cada PATCH que casar com o caminho. */
function captureBodies(path: string) {
  const bodies: unknown[] = []
  server.use(
    http.patch(path, async ({ request }) => {
      bodies.push(await request.json())
      return new HttpResponse(null, { status: 204 })
    })
  )
  return bodies
}

function appointments(list: Appointment[]) {
  server.use(http.get('*/admin/barbers/:barberId/appointments', () => HttpResponse.json(list)))
}

const completed = (overrides: Partial<Appointment>): Appointment => ({
  ...mockBarberAppointments[0],
  status: 'Completed',
  services: [
    { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45 },
    { id: 'service-2', name: 'Barba', durationMinutes: 20, price: 35 },
  ],
  effectiveAmount: 80,
  ...overrides,
})

/** Abre a escolha de pagamento ao concluir o atendimento de Pedro Costa. */
async function openCompletion(user: ReturnType<typeof userEvent.setup>) {
  render(<AdminAppointmentsSection barberId="barber-1" />)
  await screen.findByText('Pedro Costa')
  await user.click(screen.getAllByRole('button', { name: 'Concluir' })[0])
}

// O handler MSW devolve mockBarberAppointments, cujo primeiro item está Accepted.
describe('AdminAppointmentsSection', () => {
  it('mostra os confirmados por padrão, com nome do cliente', async () => {
    render(<AdminAppointmentsSection barberId="barber-1" />)
    expect(await screen.findByText('Pedro Costa')).toBeInTheDocument()
  })

  it('oferece concluir e cancelar num atendimento confirmado', async () => {
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await screen.findByText('Pedro Costa')

    expect(screen.getAllByRole('button', { name: 'Concluir' }).length).toBeGreaterThan(0)
    expect(screen.getAllByRole('button', { name: 'Cancelar' }).length).toBeGreaterThan(0)
  })

  it('pede a forma de pagamento ao concluir, com Plano entre as opções', async () => {
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await screen.findByText('Pedro Costa')

    fireEvent.click(screen.getAllByRole('button', { name: 'Concluir' })[0])

    expect(screen.getByRole('button', { name: 'Pix' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Plano' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sem registrar' })).toBeInTheDocument()
  })

  it('conclui com um método normal enviando só o método', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/admin/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: 'Cartão' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Cartão' }]))
  })

  it('conclui sem registrar pagamento', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/admin/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: 'Sem registrar' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: null }]))
  })

  it('Plano → Pagamento conclui com valor e forma, validando antes de enviar', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/admin/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: 'Plano' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: /^Pagamento/ }))
    const amount = within(dialog).getByLabelText('Valor cobrado (R$)')
    await user.type(amount, '-1')
    await user.click(within(dialog).getByRole('button', { name: /Pix/ }))
    expect(within(dialog).getByRole('alert')).toHaveTextContent('O valor não pode ser negativo.')
    expect(within(dialog).getByRole('button', { name: 'Concluir atendimento' })).toBeDisabled()

    await user.clear(amount)
    await user.type(amount, '200')
    await user.click(within(dialog).getByRole('button', { name: 'Concluir atendimento' }))

    await waitFor(() =>
      expect(bodies).toEqual([
        { paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Pix', chargedAmount: 200 },
      ])
    )
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('Plano → Recorrência conclui sem valor nem forma de pagamento', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/admin/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: 'Plano' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^Recorrência/ }))
    await user.click(screen.getByRole('button', { name: 'Concluir atendimento' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Plano', planKind: 'Recorrencia' }]))
  })

  it('registra o plano depois num concluído sem pagamento', async () => {
    const user = userEvent.setup()
    appointments([completed({ id: 'appt-unpaid', clientName: 'Paulo Pendente', paymentMethod: null })])
    const bodies = captureBodies('*/admin/appointments/:id/payment')
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await user.click(await screen.findByRole('button', { name: /Concluídos/ }))

    await user.click(screen.getByRole('button', { name: 'Registrar pagamento' }))
    await user.click(screen.getByRole('button', { name: 'Plano' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^Recorrência/ }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Registrar pagamento' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Plano', planKind: 'Recorrencia' }]))
  })

  it('mostra nos concluídos o plano, o valor efetivo e os serviços', async () => {
    const user = userEvent.setup()
    appointments([
      completed({
        id: 'appt-plan-pay',
        clientName: 'Lucas Plano',
        paymentMethod: 'Plano',
        planKind: 'Pagamento',
        planTender: 'Cartão',
        chargedAmount: 150,
        effectiveAmount: 150,
      }),
      completed({
        id: 'appt-plan-rec',
        clientName: 'Rita Recorrente',
        paymentMethod: 'Plano',
        planKind: 'Recorrencia',
        planTender: null,
        chargedAmount: 0,
        effectiveAmount: 0,
      }),
    ])
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await user.click(await screen.findByRole('button', { name: /Concluídos \(2\)/ }))

    const lucas = screen.getByText('Lucas Plano').closest('div.flex-col') as HTMLElement
    expect(within(lucas).getByText('Plano · Pagamento · Cartão')).toBeInTheDocument()
    expect(within(lucas).getByText(/R\$\s*150,00/)).toBeInTheDocument()
    expect(within(lucas).getByText('Corte Clássico + Barba')).toBeInTheDocument()

    const rita = screen.getByText('Rita Recorrente').closest('div.flex-col') as HTMLElement
    expect(within(rita).getByText('Plano · Recorrência')).toBeInTheDocument()
    expect(within(rita).getByText(/R\$\s*0,00/)).toBeInTheDocument()
    expect(within(rita).getByText('Corte Clássico + Barba')).toBeInTheDocument()
  })

  it('confirma antes de cancelar e não cancela se o admin desistir', async () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false)
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await screen.findByText('Pedro Costa')

    fireEvent.click(screen.getAllByRole('button', { name: 'Cancelar' })[0])

    expect(confirmSpy).toHaveBeenCalled()
    // Segue na lista de confirmados: nada foi cancelado.
    expect(screen.getAllByRole('button', { name: 'Concluir' }).length).toBeGreaterThan(0)
    confirmSpy.mockRestore()
  })

  it('troca de aba e mostra vazio quando não há atendimento naquela situação', async () => {
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await screen.findByText('Pedro Costa')

    fireEvent.click(screen.getByRole('button', { name: /Cancelados/ }))

    await waitFor(() => {
      expect(screen.getByText(/Nenhum atendimento nesta situação/i)).toBeInTheDocument()
    })
  })
})
