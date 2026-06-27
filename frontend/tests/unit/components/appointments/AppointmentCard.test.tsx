import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AppointmentCard } from '@/components/appointments/AppointmentCard'
import type { Appointment } from '@/types/api.types'

const mockAppointment: Appointment = {
  id: 'appt-1',
  clientName: 'João Silva',
  clientPhone: '+5511999990000',
  barberId: 'barber-1',
  barberName: 'Carlos Andrade',
  scheduledAt: '2024-06-15T14:30:00.000Z',
  totalDurationMinutes: 30,
  status: 'Accepted',
  notes: 'Manter a lateral curta',
  createdAt: '2024-06-10T10:00:00.000Z',
  services: [
    { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    { id: 'service-2', name: 'Barba', durationMinutes: 20, price: 35.0 },
  ],
}

describe('AppointmentCard', () => {
  it('renders without crashing', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(
      screen.getByRole('article', { name: /Carlos Andrade/i })
    ).toBeInTheDocument()
  })

  it('displays the barber name', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText('Carlos Andrade')).toBeInTheDocument()
  })

  it('displays the client name', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText(/João Silva/)).toBeInTheDocument()
  })

  it('displays the client phone', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText(/\+5511999990000/)).toBeInTheDocument()
  })

  it('displays all service names', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText('Corte Clássico')).toBeInTheDocument()
    expect(screen.getByText('Barba')).toBeInTheDocument()
  })

  it('displays the appointment notes', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText(/"Manter a lateral curta"/)).toBeInTheDocument()
  })

  it('does not render notes section when notes is null', () => {
    const apptWithoutNotes = { ...mockAppointment, notes: null }
    render(<AppointmentCard appointment={apptWithoutNotes} />)
    expect(screen.queryByRole('figure')).not.toBeInTheDocument()
  })

  it('displays the status badge', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    expect(screen.getByText('Confirmado')).toBeInTheDocument()
  })

  it('renders action buttons when actions prop is provided', () => {
    render(
      <AppointmentCard
        appointment={mockAppointment}
        actions={<button>Ação</button>}
      />
    )
    expect(screen.getByRole('button', { name: 'Ação' })).toBeInTheDocument()
  })

  it('does not render actions section when actions prop is not provided', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    // No extra buttons should be present
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('displays total price for all services', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    // Total: 45 + 35 = 80
    expect(screen.getByText(/R\$\s*80/)).toBeInTheDocument()
  })
})
