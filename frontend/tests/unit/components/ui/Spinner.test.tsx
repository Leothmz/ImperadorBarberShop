import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Spinner } from '@/components/ui/Spinner'

describe('Spinner', () => {
  it('renders with accessible role and label', () => {
    render(<Spinner />)
    expect(screen.getByRole('status', { name: 'Carregando...' })).toBeInTheDocument()
  })

  it('applies small size class', () => {
    const { container } = render(<Spinner size="sm" />)
    // SVG className is SVGAnimatedString; use getAttribute instead
    expect(container.querySelector('svg')?.getAttribute('class')).toContain('h-4')
  })

  it('applies large size class', () => {
    const { container } = render(<Spinner size="lg" />)
    expect(container.querySelector('svg')?.getAttribute('class')).toContain('h-12')
  })

  it('applies custom className', () => {
    const { container } = render(<Spinner className="my-custom" />)
    expect(container.querySelector('svg')?.getAttribute('class')).toContain('my-custom')
  })
})
