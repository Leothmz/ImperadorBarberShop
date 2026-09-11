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
  paymentMethod: null,
  paidAt: null,
  planKind: null,
  planTender: null,
  chargedAmount: null,
  effectiveAmount: 80.0,
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

  it('displays the effective amount, the sum of the booked services outside the plan', () => {
    render(<AppointmentCard appointment={mockAppointment} />)
    // Total: 45 + 35 = 80
    expect(screen.getByText(/R\$\s*80/)).toBeInTheDocument()
  })

  it('shows a normal payment method on a completed appointment', () => {
    render(<AppointmentCard appointment={{ ...mockAppointment, status: 'Completed', paymentMethod: 'Cartão' }} />)
    expect(screen.getByText('Cartão', { exact: false })).toHaveTextContent('💳 Cartão')
    expect(screen.queryByText(/cobrados no plano|Coberto pelo plano/)).not.toBeInTheDocument()
  })

  it('shows the plan payment details, its charged amount and keeps the services', () => {
    render(
      <AppointmentCard
        appointment={{
          ...mockAppointment,
          status: 'Completed',
          paymentMethod: 'Plano',
          planKind: 'Pagamento',
          planTender: 'Pix',
          chargedAmount: 150,
          effectiveAmount: 150,
        }}
      />
    )
    expect(screen.getByText('Plano · Pagamento · Pix', { exact: false })).toHaveTextContent('📋 Plano · Pagamento · Pix')
    expect(screen.getByText(/R\$\s*150,00 cobrados no plano/)).toBeInTheDocument()
    expect(screen.getByText('Corte Clássico')).toBeInTheDocument()
    expect(screen.getByText('Barba')).toBeInTheDocument()
    expect(screen.queryByText(/R\$\s*80/)).not.toBeInTheDocument()
  })

  it('shows a plan recurrence at zero, services still listed', () => {
    render(
      <AppointmentCard
        appointment={{
          ...mockAppointment,
          status: 'Completed',
          paymentMethod: 'Plano',
          planKind: 'Recorrencia',
          chargedAmount: 0,
          effectiveAmount: 0,
        }}
      />
    )
    expect(screen.getByText('Plano · Recorrência', { exact: false })).toBeInTheDocument()
    expect(screen.getByText(/Coberto pelo plano: R\$\s*0,00 nesta visita/)).toBeInTheDocument()
    expect(screen.getByText('Corte Clássico')).toBeInTheDocument()
  })

  it('marks a completed appointment without payment', () => {
    render(<AppointmentCard appointment={{ ...mockAppointment, status: 'Completed' }} />)
    expect(screen.getByText('— sem método')).toBeInTheDocument()
  })
})
