import { describe, it, expect } from 'vitest'
import { render, screen } from '../test-utils'
import LandingPage from '@/app/page'

describe('LandingPage', () => {
  it('renders logo', () => {
    render(<LandingPage />)
    expect(screen.getByAltText('O Imperador Barber Shop')).toBeInTheDocument()
  })

  it('renders Área do Barbeiro links pointing to /login', () => {
    render(<LandingPage />)
    const links = screen.getAllByRole('link', { name: /área do barbeiro/i })
    expect(links.length).toBeGreaterThanOrEqual(1)
    links.forEach((link) => expect(link).toHaveAttribute('href', '/login'))
  })

  it('does not render Sou barbeiro link', () => {
    render(<LandingPage />)
    expect(screen.queryByText(/sou barbeiro/i)).not.toBeInTheDocument()
  })
})
