import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor, fireEvent } from '../../test-utils'
import { ServicePicker } from '@/components/booking/ServicePicker'

describe('ServicePicker', () => {
  it('shows loading spinner initially', () => {
    render(<ServicePicker selectedServiceIds={[]} onToggle={vi.fn()} />)
    expect(screen.getByRole('status')).toBeInTheDocument()
  })

  it('renders active services after loading', async () => {
    render(<ServicePicker selectedServiceIds={[]} onToggle={vi.fn()} />)
    await waitFor(() => {
      expect(screen.getByText('Corte Clássico')).toBeInTheDocument()
      expect(screen.getByText('Barba')).toBeInTheDocument()
      expect(screen.getByText('Corte + Barba')).toBeInTheDocument()
      // Inactive service should NOT be shown
      expect(screen.queryByText('Hidratação')).not.toBeInTheDocument()
    })
  })

  it('calls onToggle when a service checkbox is clicked', async () => {
    const handleToggle = vi.fn()
    render(<ServicePicker selectedServiceIds={[]} onToggle={handleToggle} />)
    await waitFor(() => screen.getByText('Corte Clássico'))
    const checkbox = screen.getByLabelText('Selecionar Corte Clássico')
    fireEvent.click(checkbox)
    expect(handleToggle).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Corte Clássico' })
    )
  })

  it('shows selected state for checked services', async () => {
    render(<ServicePicker selectedServiceIds={['service-1']} onToggle={vi.fn()} />)
    await waitFor(() => screen.getByText('Corte Clássico'))
    const checkbox = screen.getByLabelText('Selecionar Corte Clássico')
    expect(checkbox).toBeChecked()
  })

  it('shows the running total when services are selected', async () => {
    render(
      <ServicePicker selectedServiceIds={['service-1', 'service-2']} onToggle={vi.fn()} />
    )
    await waitFor(() => {
      // 45 + 35 = 80
      expect(screen.getByText(/R\$\s*80/)).toBeInTheDocument()
    })
  })

  it('does not show total footer when no services are selected', async () => {
    render(<ServicePicker selectedServiceIds={[]} onToggle={vi.fn()} />)
    await waitFor(() => screen.getByText('Corte Clássico'))
    // No total bar
    expect(screen.queryByLabelText('Total dos serviços selecionados')).not.toBeInTheDocument()
  })
})
