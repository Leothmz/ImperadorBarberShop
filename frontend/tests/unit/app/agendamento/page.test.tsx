import { describe, it, expect } from 'vitest'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { render, screen, waitFor } from '../../test-utils'
import { server } from '../../../mocks/server'
import { mockManagedAppointment } from '../../../mocks/handlers'
import { ManageAppointmentView } from '@/app/agendamento/[token]/ManageAppointmentView'

const BASE_URL = 'http://localhost:5000/api/v1'

function respondWith(appointment: Record<string, unknown>) {
  server.use(
    http.get(`${BASE_URL}/appointments/manage/:token`, () => HttpResponse.json(appointment))
  )
}

describe('ManageAppointmentPage', () => {
  it('renders the appointment summary for a valid token', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => {
      expect(screen.getByText('Carlos Andrade')).toBeInTheDocument()
      expect(screen.getByText('João Silva')).toBeInTheDocument()
    })
  })

  it('shows a cancel button for an Accepted appointment scheduled far in the future', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Cancelar agendamento' })).toBeEnabled()
    })
  })

  it('opens as a confirmation right after booking, telling the client to keep the link', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" isNew />)

    await waitFor(() => {
      expect(screen.getByText('Agendamento confirmado')).toBeInTheDocument()
    })
    // O link é o único acesso do cliente ao agendamento — a página tem que pedir
    // que ele seja guardado, não só redirecionar para cá.
    expect(screen.getByText(/guarde este link/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar link' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Adicionar à agenda' })).toBeInTheDocument()
  })

  it('does not shout "confirmado" at someone opening an old link', async () => {
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => screen.getByText('Carlos Andrade'))
    expect(screen.queryByText('Agendamento confirmado')).not.toBeInTheDocument()
  })

  it('asks for confirmation before destroying the booking', async () => {
    const user = userEvent.setup()
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => screen.getByRole('button', { name: 'Cancelar agendamento' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar agendamento' }))

    const dialog = await screen.findByRole('dialog')
    expect(dialog).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Manter agendamento' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sim, cancelar' })).toBeInTheDocument()
  })

  it('surfaces a failed cancellation instead of looking like it worked', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(`${BASE_URL}/appointments/manage/:token/cancel`, () =>
        HttpResponse.json({ message: 'nope' }, { status: 500 })
      )
    )
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => screen.getByRole('button', { name: 'Cancelar agendamento' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar agendamento' }))
    await user.click(await screen.findByRole('button', { name: 'Sim, cancelar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/não conseguimos cancelar/i)
  })

  it('explains the 2-hour rule instead of showing a dead button', async () => {
    respondWith({
      ...mockManagedAppointment,
      scheduledAt: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
    })
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => screen.getByText('Carlos Andrade'))
    expect(screen.queryByRole('button', { name: /cancelar agendamento/i })).not.toBeInTheDocument()
    expect(screen.getByText(/fecha 2 horas antes/i)).toBeInTheDocument()
  })

  it('offers a way back into booking after a cancellation', async () => {
    respondWith({ ...mockManagedAppointment, status: 'Cancelled' })
    render(<ManageAppointmentView token="mock-access-token-1" />)

    await waitFor(() => screen.getByText(/foi cancelado/i))
    expect(screen.getByRole('link', { name: 'Agendar novo horário' })).toHaveAttribute(
      'href',
      '/agendar'
    )
  })

  it('offers a door, not a wall, when the link is invalid', async () => {
    server.use(
      http.get(`${BASE_URL}/appointments/manage/:token`, () => new HttpResponse(null, { status: 404 }))
    )
    render(<ManageAppointmentView token="token-invalido" />)

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())
    expect(screen.getByRole('link', { name: /novo agendamento/i })).toHaveAttribute(
      'href',
      '/agendar'
    )
  })
})
