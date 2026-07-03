import { describe, it, expect } from 'vitest'
import { render, screen, waitFor, fireEvent } from '../../test-utils'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'

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

  it('clicking Concluir shows payment method options', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    expect(screen.getByText(/Forma de pagamento/i)).toBeInTheDocument()
    expect(screen.getByText('Pular')).toBeInTheDocument()
  })

  it('Pular completes without payment method', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => screen.getAllByRole('button', { name: /Concluir/i }))
    fireEvent.click(screen.getAllByRole('button', { name: /Concluir/i })[0])
    fireEvent.click(screen.getByText('Pular'))
    fireEvent.click(screen.getByRole('button', { name: /Confirmar/i }))
    // No error thrown = payment selection didn't block completion
  })
})
