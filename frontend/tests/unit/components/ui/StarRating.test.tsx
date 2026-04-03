import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { StarRatingDisplay, StarRatingInput } from '@/components/ui/StarRating'

describe('StarRatingDisplay', () => {
  it('renders without crashing', () => {
    render(<StarRatingDisplay rating={4} />)
    expect(screen.getByLabelText('Avaliação: 4 de 5')).toBeInTheDocument()
  })

  it('renders 5 stars by default', () => {
    const { container } = render(<StarRatingDisplay rating={3} />)
    // Each star is an SVG
    const svgs = container.querySelectorAll('svg')
    expect(svgs).toHaveLength(5)
  })

  it('renders correct number of stars for custom maxStars', () => {
    const { container } = render(<StarRatingDisplay rating={2} maxStars={3} />)
    const svgs = container.querySelectorAll('svg')
    expect(svgs).toHaveLength(3)
  })

  it('shows correct aria-label for zero rating', () => {
    render(<StarRatingDisplay rating={0} />)
    expect(screen.getByLabelText('Avaliação: 0 de 5')).toBeInTheDocument()
  })
})

describe('StarRatingInput', () => {
  it('renders 5 clickable star buttons', () => {
    render(<StarRatingInput value={0} onChange={vi.fn()} />)
    const buttons = screen.getAllByRole('button')
    expect(buttons).toHaveLength(5)
  })

  it('calls onChange with the clicked star value', () => {
    const handleChange = vi.fn()
    render(<StarRatingInput value={0} onChange={handleChange} />)
    fireEvent.click(screen.getByLabelText('3 estrelas'))
    expect(handleChange).toHaveBeenCalledWith(3)
  })

  it('marks the correct stars as pressed', () => {
    render(<StarRatingInput value={3} onChange={vi.fn()} />)
    expect(screen.getByLabelText('1 estrela')).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('2 estrelas')).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('3 estrelas')).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByLabelText('4 estrelas')).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByLabelText('5 estrelas')).toHaveAttribute('aria-pressed', 'false')
  })

  it('renders the accessible group label', () => {
    render(<StarRatingInput value={0} onChange={vi.fn()} />)
    expect(screen.getByRole('group', { name: 'Selecione uma avaliação' })).toBeInTheDocument()
  })
})
