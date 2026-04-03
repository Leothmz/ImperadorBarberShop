import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor } from '../../test-utils'
import { BarberPicker } from '@/components/booking/BarberPicker'

describe('BarberPicker', () => {
  it('shows loading spinner initially', () => {
    render(<BarberPicker selectedBarberId={null} onSelect={vi.fn()} />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders barbers after loading', async () => {
    render(<BarberPicker selectedBarberId={null} onSelect={vi.fn()} />)
    await waitFor(() => {
      expect(screen.getByText('Carlos Andrade')).toBeInTheDocument()
      expect(screen.getByText('Rafael Lima')).toBeInTheDocument()
    })
  })

  it('calls onSelect when a barber card is clicked', async () => {
    const handleSelect = vi.fn()
    render(<BarberPicker selectedBarberId={null} onSelect={handleSelect} />)
    await waitFor(() => screen.getByText('Carlos Andrade'))
    screen.getAllByRole('listitem')[0].click()
    expect(handleSelect).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Carlos Andrade' })
    )
  })

  it('marks the selected barber with aria-pressed true', async () => {
    render(<BarberPicker selectedBarberId="barber-1" onSelect={vi.fn()} />)
    await waitFor(() => screen.getByText('Carlos Andrade'))
    const buttons = screen.getAllByRole('listitem')
    const carlosButton = buttons.find((b) => b.textContent?.includes('Carlos Andrade'))
    expect(carlosButton).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows "Selecionado" text for selected barber', async () => {
    render(<BarberPicker selectedBarberId="barber-1" onSelect={vi.fn()} />)
    await waitFor(() => {
      expect(screen.getByText('Selecionado')).toBeInTheDocument()
    })
  })
})
