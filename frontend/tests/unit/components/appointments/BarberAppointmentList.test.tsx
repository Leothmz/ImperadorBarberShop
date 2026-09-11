import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import userEvent from '@testing-library/user-event'
import { render, screen, waitFor, fireEvent, within } from '../../test-utils'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'
import type { Appointment } from '@/types/api.types'
import { mockBarberAppointments } from '../../../mocks/handlers'
import { server } from '../../../mocks/server'

/** Guarda o corpo de cada PATCH que casar com o caminho (sem corpo vira `undefined`). */
function captureBodies(path: string) {
  const bodies: unknown[] = []
  server.use(
    http.patch(path, async ({ request }) => {
      const text = await request.text()
      bodies.push(text ? JSON.parse(text) : undefined)
      return new HttpResponse(null, { status: 204 })
    })
  )
  return bodies
}

function agenda(list: Appointment[]) {
  server.use(http.get('*/appointments/barber', () => HttpResponse.json(list)))
}

const corteEBarba = [
  { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45 },
  { id: 'service-2', name: 'Barba', durationMinutes: 20, price: 35 },
]

const completed = (overrides: Partial<Appointment>): Appointment => ({
  ...mockBarberAppointments[0],
  status: 'Completed',
  services: corteEBarba,
  effectiveAmount: 80,
  ...overrides,
})

/** Abre a escolha de pagamento do primeiro atendimento confirmado (Pedro Costa). */
async function openCompletion(user: ReturnType<typeof userEvent.setup>) {
  render(<BarberAppointmentList />)
  const [first] = await screen.findAllByRole('article')
  await user.click(within(first).getByRole('button', { name: 'Concluir' }))
}

describe('BarberAppointmentList', () => {
  it('shows loading spinner initially', () => {
    render(<BarberAppointmentList />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders appointments after loading', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getByText(/Pedro Costa/)).toBeInTheDocument()
    })
  })

  it('shows Concluir button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Concluir/i }).length).toBeGreaterThan(0)
    })
  })

  it('clicking Concluir shows payment method options, Plano included', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    expect(screen.getByText(/Forma de pagamento/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Plano/ })).toBeInTheDocument()
    expect(screen.getByText('Pular')).toBeInTheDocument()
  })

  it('Pular completes without payment method', async () => {
    const bodies = captureBodies('*/appointments/:id/complete')
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    fireEvent.click(screen.getByText('Pular'))
    fireEvent.click(screen.getByRole('button', { name: /Confirmar/i }))

    await waitFor(() => expect(bodies).toEqual([undefined]))
  })

  it('a normal method completes with just that method', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: /Pix/ }))
    await user.click(screen.getByRole('button', { name: 'Confirmar' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Pix' }]))
  })

  it('Plano → Pagamento completes with the amount and the tender', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: /Plano/ }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: /^Pagamento/ }))
    const confirm = within(dialog).getByRole('button', { name: 'Concluir atendimento' })
    expect(confirm).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Valor cobrado (R$)'), '150')
    await user.click(within(dialog).getByRole('button', { name: /Dinheiro/ }))
    await user.click(confirm)

    await waitFor(() =>
      expect(bodies).toEqual([
        { paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Dinheiro', chargedAmount: 150 },
      ])
    )
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('Plano → Recorrência completes at zero without a tender', async () => {
    const user = userEvent.setup()
    const bodies = captureBodies('*/appointments/:id/complete')
    await openCompletion(user)

    await user.click(screen.getByRole('button', { name: /Plano/ }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /^Recorrência/ }))
    await user.click(screen.getByRole('button', { name: 'Concluir atendimento' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Plano', planKind: 'Recorrencia' }]))
  })

  it('registers a plan later on a completed appointment without payment', async () => {
    const user = userEvent.setup()
    agenda([completed({ id: 'appt-unpaid', clientName: 'Paulo Pendente', paymentMethod: null })])
    const bodies = captureBodies('*/appointments/:id/payment')
    render(<BarberAppointmentList />)

    await user.click(await screen.findByRole('button', { name: 'Registrar pagamento' }))
    await user.click(screen.getByRole('button', { name: 'Plano' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: /^Pagamento/ }))
    await user.type(within(dialog).getByLabelText('Valor cobrado (R$)'), '99,90')
    await user.click(within(dialog).getByRole('button', { name: /Pix/ }))
    await user.click(within(dialog).getByRole('button', { name: 'Registrar pagamento' }))

    await waitFor(() =>
      expect(bodies).toEqual([
        { paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Pix', chargedAmount: 99.9 },
      ])
    )
  })

  it('registers a normal method later with just that method', async () => {
    const user = userEvent.setup()
    agenda([completed({ id: 'appt-unpaid', paymentMethod: null })])
    const bodies = captureBodies('*/appointments/:id/payment')
    render(<BarberAppointmentList />)

    await user.click(await screen.findByRole('button', { name: 'Registrar pagamento' }))
    await user.click(screen.getByRole('button', { name: 'Cartão' }))

    await waitFor(() => expect(bodies).toEqual([{ paymentMethod: 'Cartão' }]))
  })

  it('shows the recorded plan details and amount without hiding the services', async () => {
    agenda([
      completed({
        id: 'appt-plan-pay',
        clientName: 'Lucas Plano',
        paymentMethod: 'Plano',
        planKind: 'Pagamento',
        planTender: 'Pix',
        chargedAmount: 120,
        effectiveAmount: 120,
      }),
      completed({
        id: 'appt-plan-rec',
        clientName: 'Rita Recorrente',
        scheduledAt: new Date(Date.now() + 86400000 * 2).toISOString(),
        paymentMethod: 'Plano',
        planKind: 'Recorrencia',
        planTender: null,
        chargedAmount: 0,
        effectiveAmount: 0,
      }),
    ])
    render(<BarberAppointmentList />)

    const [payment, recurrence] = await screen.findAllByRole('article')
    expect(payment).toHaveTextContent('Lucas Plano')
    expect(within(payment).getByText('Plano · Pagamento · Pix', { exact: false })).toBeInTheDocument()
    expect(within(payment).getByText(/R\$\s*120,00 cobrados no plano/)).toBeInTheDocument()
    expect(within(payment).getByText('Corte Clássico')).toBeInTheDocument()
    expect(within(payment).getByText('Barba')).toBeInTheDocument()

    expect(recurrence).toHaveTextContent('Rita Recorrente')
    expect(within(recurrence).getByText('Plano · Recorrência', { exact: false })).toBeInTheDocument()
    expect(within(recurrence).getByText(/Coberto pelo plano: R\$\s*0,00 nesta visita/)).toBeInTheDocument()
    expect(within(recurrence).getByText('Corte Clássico')).toBeInTheDocument()
    // Plano já registrado: não oferece registrar de novo
    expect(screen.queryByRole('button', { name: 'Registrar pagamento' })).not.toBeInTheDocument()
  })
})
