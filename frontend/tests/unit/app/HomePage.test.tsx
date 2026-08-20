import { describe, it, expect } from 'vitest'
import { render, screen, waitFor } from '../test-utils'
import LandingPage from '@/app/page'
import { Footer } from '@/components/layout/Footer'

describe('LandingPage', () => {
  it('renders logo', () => {
    render(<LandingPage />)
    expect(screen.getByAltText('O Imperador Barber Shop')).toBeInTheDocument()
  })

  it('points every call to action at the booking wizard', () => {
    render(<LandingPage />)
    const links = screen.getAllByRole('link', { name: /agendar agora/i })

    expect(links.length).toBeGreaterThanOrEqual(1)
    links.forEach((link) => expect(link).toHaveAttribute('href', '/agendar'))
  })

  it('keeps the staff entrance out of the client path', () => {
    render(<LandingPage />)
    // Quase todo o tráfego é cliente vindo do Instagram: a porta do barbeiro
    // mora no rodapé, não disputando espaço com o CTA de agendar.
    expect(screen.queryByRole('link', { name: /área do barbeiro/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/sou barbeiro/i)).not.toBeInTheDocument()
  })

  it('claims nothing the shop cannot back up', () => {
    render(<LandingPage />)
    const text = document.body.textContent ?? ''

    // A barbearia não tem foto, depoimento nem avaliação publicada; a página não
    // pode alegar tempo de casa nem exibir avaliações que não mostra.
    expect(text).not.toMatch(/anos de experiência/i)
    expect(text).not.toMatch(/avaliações reais/i)
    expect(text).not.toMatch(/melhores profissionais/i)
  })

  it('leads with the differentiator that is actually true', () => {
    render(<LandingPage />)
    expect(screen.getByText(/sem criar conta/i)).toBeInTheDocument()
    expect(screen.getByText(/não pede cadastro/i)).toBeInTheDocument()
  })

  it('explains the link and the 2-hour rule before anyone books', () => {
    render(<LandingPage />)
    expect(screen.getByText(/guarde o link/i)).toBeInTheDocument()
    expect(screen.getByText(/até 2 horas antes/i)).toBeInTheDocument()
  })

  it('builds the price board from the real catalog', async () => {
    render(<LandingPage />)

    await waitFor(() => {
      expect(screen.getByText('Corte Clássico')).toBeInTheDocument()
    })
    expect(screen.getByText('R$ 45,00')).toBeInTheDocument()
  })

  it('sends each real barber straight into their own agenda', async () => {
    render(<LandingPage />)

    const carlos = await screen.findByRole('link', { name: /carlos andrade/i })
    expect(carlos).toHaveAttribute('href', '/agendar?barbeiro=barber-1')
  })

  it('says a barber has no ratings instead of inventing one', async () => {
    render(<LandingPage />)
    await screen.findByRole('link', { name: /carlos andrade/i })

    // mockBarbers trazem nota real; a ausência só aparece quando averageRating é 0.
    expect(screen.queryByText(/ainda sem avaliações/i)).not.toBeInTheDocument()
    expect(screen.getByText('4.8')).toBeInTheDocument()
  })
})

describe('Footer', () => {
  it('carries the barber entrance', () => {
    render(<Footer />)
    expect(screen.getByRole('link', { name: /área do barbeiro/i })).toHaveAttribute(
      'href',
      '/login'
    )
  })
})
