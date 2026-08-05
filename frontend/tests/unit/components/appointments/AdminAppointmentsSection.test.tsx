import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor, fireEvent } from '../../test-utils'
import AdminAppointmentsSection from '@/app/admin/barbers/AdminAppointmentsSection'

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

  it('pede a forma de pagamento ao concluir', async () => {
    render(<AdminAppointmentsSection barberId="barber-1" />)
    await screen.findByText('Pedro Costa')

    fireEvent.click(screen.getAllByRole('button', { name: 'Concluir' })[0])

    expect(screen.getByRole('button', { name: 'Pix' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sem registrar' })).toBeInTheDocument()
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
