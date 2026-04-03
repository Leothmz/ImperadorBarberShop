import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'

describe('BarberAppointmentList', () => {
  it('shows loading spinner initially', () => {
    render(<BarberAppointmentList />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders appointments after loading', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      // Client names appear inside "Cliente: <name>" paragraphs split across elements
      expect(screen.getByText(/Pedro Costa/)).toBeInTheDocument()
      expect(screen.getByText(/Maria Santos/)).toBeInTheDocument()
    })
  })

  it('shows Aceitar button for pending appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Aceitar/i })).toBeInTheDocument()
    })
  })

  it('shows Recusar button for pending appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Recusar/i })).toBeInTheDocument()
    })
  })

  it('shows Concluir button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Concluir/i })).toBeInTheDocument()
    })
  })
})
