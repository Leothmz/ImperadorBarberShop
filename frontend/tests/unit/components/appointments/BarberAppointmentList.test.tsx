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
      expect(screen.getByText(/Pedro Costa/)).toBeInTheDocument()
      expect(screen.getByText(/Maria Santos/)).toBeInTheDocument()
    })
  })

  it('shows Concluir button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Concluir/i })).toHaveLength(2)
    })
  })

  it('shows Cancelar button for accepted appointments', async () => {
    render(<BarberAppointmentList />)
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /Cancelar/i })).toHaveLength(2)
    })
  })
})
