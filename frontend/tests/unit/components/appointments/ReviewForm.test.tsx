import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '../../test-utils'
import { ReviewForm } from '@/components/appointments/ReviewForm'

describe('ReviewForm', () => {
  it('renders without crashing', () => {
    render(<ReviewForm appointmentId="appt-1" onSuccess={vi.fn()} />)
    expect(screen.getByText('Como você avalia o serviço?')).toBeInTheDocument()
  })

  it('renders 5 star buttons', () => {
    render(<ReviewForm appointmentId="appt-1" onSuccess={vi.fn()} />)
    expect(screen.getAllByRole('button').filter((b) => b.getAttribute('aria-label')?.includes('estrela'))).toHaveLength(5)
  })

  it('shows an error when submitting without selecting a rating', async () => {
    render(<ReviewForm appointmentId="appt-1" onSuccess={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: 'Enviar avaliação' }))
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Selecione uma avaliação')
    })
  })

  it('calls onSuccess after a successful submission', async () => {
    const handleSuccess = vi.fn()
    render(<ReviewForm appointmentId="appt-1" onSuccess={handleSuccess} />)

    // Select 5 stars
    fireEvent.click(screen.getByLabelText('5 estrelas'))
    fireEvent.click(screen.getByRole('button', { name: 'Enviar avaliação' }))

    await waitFor(() => {
      expect(handleSuccess).toHaveBeenCalledOnce()
    })
  })

  it('allows entering a comment', () => {
    render(<ReviewForm appointmentId="appt-1" onSuccess={vi.fn()} />)
    const textarea = screen.getByLabelText(/Comentário/)
    fireEvent.change(textarea, { target: { value: 'Excelente serviço!' } })
    expect(textarea).toHaveValue('Excelente serviço!')
  })

  it('shows label text for selected rating', async () => {
    render(<ReviewForm appointmentId="appt-1" onSuccess={vi.fn()} />)
    fireEvent.click(screen.getByLabelText('5 estrelas'))
    await waitFor(() => {
      expect(screen.getByText('Excelente')).toBeInTheDocument()
    })
  })
})
