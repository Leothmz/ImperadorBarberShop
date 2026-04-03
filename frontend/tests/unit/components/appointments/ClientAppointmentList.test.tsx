import { describe, it, expect } from 'vitest'
import { render, screen, waitFor, fireEvent } from '../../test-utils'
import { ClientAppointmentList } from '@/components/appointments/ClientAppointmentList'

describe('ClientAppointmentList', () => {
  it('shows loading spinner initially', () => {
    render(<ClientAppointmentList />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders the Próximos tab as active by default', async () => {
    render(<ClientAppointmentList />)
    await waitFor(() => {
      const proximos = screen.getByRole('button', { name: /Próximos/i })
      expect(proximos).toHaveAttribute('aria-pressed', 'true')
    })
  })

  it('switches to Histórico tab when clicked', async () => {
    render(<ClientAppointmentList />)
    await waitFor(() => screen.getByRole('button', { name: /Histórico/i }))

    fireEvent.click(screen.getByRole('button', { name: /Histórico/i }))

    expect(screen.getByRole('button', { name: /Histórico/i })).toHaveAttribute(
      'aria-pressed',
      'true'
    )
  })

  it('shows Avaliar button for completed appointments in history', async () => {
    render(<ClientAppointmentList />)
    await waitFor(() => screen.getByRole('button', { name: /Histórico/i }))

    // Switch to history tab
    fireEvent.click(screen.getByRole('button', { name: /Histórico/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Avaliar/i })).toBeInTheDocument()
    })
  })

  it('opens review modal when Avaliar is clicked', async () => {
    render(<ClientAppointmentList />)
    await waitFor(() => screen.getByRole('button', { name: /Histórico/i }))

    fireEvent.click(screen.getByRole('button', { name: /Histórico/i }))

    await waitFor(() => screen.getByRole('button', { name: /Avaliar/i }))
    fireEvent.click(screen.getByRole('button', { name: /Avaliar/i }))

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument()
      expect(screen.getByText('Avaliar serviço')).toBeInTheDocument()
    })
  })
})
