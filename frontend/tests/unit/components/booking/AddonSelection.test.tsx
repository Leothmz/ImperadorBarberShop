import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '../../test-utils'
import { ServicePicker } from '@/components/booking/ServicePicker'

describe('ServicePicker add-ons', () => {
  it('shows add-on section when selected service has addons', async () => {
    render(
      <ServicePicker
        selectedServiceIds={['service-1']}
        onToggle={vi.fn()}
        onToggleAddon={vi.fn()}
      />
    )
    await waitFor(() => {
      expect(screen.getByText('Deseja adicionar?')).toBeInTheDocument()
      // The addon name shown (from mockServices service-1's addon)
      expect(screen.getAllByText('Barba').length).toBeGreaterThanOrEqual(1)
    })
  })

  it('does not show add-on section when selected service has no addons', async () => {
    render(
      <ServicePicker
        selectedServiceIds={['service-2']}
        onToggle={vi.fn()}
        onToggleAddon={vi.fn()}
      />
    )
    await waitFor(() => screen.getByText('Barba'))
    expect(screen.queryByText('Deseja adicionar?')).not.toBeInTheDocument()
  })

  it('does not show add-on section when no service is selected', async () => {
    render(
      <ServicePicker
        selectedServiceIds={[]}
        onToggle={vi.fn()}
        onToggleAddon={vi.fn()}
      />
    )
    await waitFor(() => screen.getByText('Corte Clássico'))
    expect(screen.queryByText('Deseja adicionar?')).not.toBeInTheDocument()
  })

  it('calls onToggleAddon when addon checkbox clicked', async () => {
    const handleToggleAddon = vi.fn()
    render(
      <ServicePicker
        selectedServiceIds={['service-1']}
        onToggle={vi.fn()}
        onToggleAddon={handleToggleAddon}
      />
    )
    await waitFor(() => screen.getByText('Deseja adicionar?'))
    // Use getAllByRole and pick the addon checkbox (aria-label="Barba", not "Selecionar Barba")
    const addonCheckboxes = screen.getAllByRole('checkbox', { name: /barba/i })
    const addonCheckbox = addonCheckboxes.find(
      (el) => el.getAttribute('aria-label') === 'Barba'
    )!
    fireEvent.click(addonCheckbox)
    expect(handleToggleAddon).toHaveBeenCalledWith('addon-barba')
  })

  it('includes addon price in total when addon is selected', async () => {
    render(
      <ServicePicker
        selectedServiceIds={['service-1', 'addon-barba']}
        onToggle={vi.fn()}
        onToggleAddon={vi.fn()}
      />
    )
    await waitFor(() => {
      // service-1 = R$ 45, addon-barba = R$ 25 → total R$ 70
      // The total footer has aria-label="Total dos serviços selecionados"
      const totalFooter = screen.getByLabelText('Total dos serviços selecionados')
      expect(totalFooter).toHaveTextContent(/R\$\s*70/)
    })
  })
})
