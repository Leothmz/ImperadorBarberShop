import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Button } from '@/components/ui/Button'

describe('Button', () => {
  it('renders with default props', () => {
    render(<Button>Clique aqui</Button>)
    expect(screen.getByRole('button', { name: 'Clique aqui' })).toBeInTheDocument()
  })

  it('calls onClick handler when clicked', () => {
    const handleClick = vi.fn()
    render(<Button onClick={handleClick}>Clique</Button>)
    fireEvent.click(screen.getByRole('button'))
    expect(handleClick).toHaveBeenCalledOnce()
  })

  it('is disabled when disabled prop is true', () => {
    render(<Button disabled>Desabilitado</Button>)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('is disabled and shows spinner when isLoading is true', () => {
    render(<Button isLoading>Carregando</Button>)
    const button = screen.getByRole('button')
    expect(button).toBeDisabled()
    // Spinner SVG is rendered (aria-hidden)
    expect(button.querySelector('svg')).toBeInTheDocument()
  })

  it('does not call onClick when disabled', () => {
    const handleClick = vi.fn()
    render(<Button disabled onClick={handleClick}>Botão</Button>)
    fireEvent.click(screen.getByRole('button'))
    expect(handleClick).not.toHaveBeenCalled()
  })

  it('renders with secondary variant classes', () => {
    render(<Button variant="secondary">Secundário</Button>)
    const button = screen.getByRole('button')
    expect(button.className).toContain('border')
  })

  it('renders with danger variant', () => {
    render(<Button variant="danger">Excluir</Button>)
    const button = screen.getByRole('button')
    expect(button.className).toContain('bg-red')
  })

  it('applies custom className', () => {
    render(<Button className="w-full">Botão</Button>)
    expect(screen.getByRole('button').className).toContain('w-full')
  })

  it('renders as type="submit" when specified', () => {
    render(<Button type="submit">Enviar</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'submit')
  })
})
