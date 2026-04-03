import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Input } from '@/components/ui/Input'

describe('Input', () => {
  it('renders without crashing', () => {
    render(<Input />)
    expect(screen.getByRole('textbox')).toBeInTheDocument()
  })

  it('renders with a label', () => {
    render(<Input label="Nome" />)
    expect(screen.getByLabelText('Nome')).toBeInTheDocument()
  })

  it('displays an error message when error prop is provided', () => {
    render(<Input label="E-mail" error="E-mail inválido" />)
    expect(screen.getByRole('alert')).toHaveTextContent('E-mail inválido')
  })

  it('sets aria-invalid when error is present', () => {
    render(<Input label="Campo" error="Obrigatório" />)
    expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'true')
  })

  it('does not set aria-invalid when there is no error', () => {
    render(<Input label="Campo" />)
    expect(screen.getByRole('textbox')).toHaveAttribute('aria-invalid', 'false')
  })

  it('displays a hint when hint prop is provided', () => {
    render(<Input label="Senha" hint="Mínimo 6 caracteres" />)
    expect(screen.getByText('Mínimo 6 caracteres')).toBeInTheDocument()
  })

  it('does not display hint when error is present', () => {
    render(<Input label="Campo" error="Erro" hint="Dica" />)
    expect(screen.queryByText('Dica')).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Erro')
  })

  it('fires onChange handler when input changes', () => {
    const handleChange = vi.fn()
    render(<Input onChange={handleChange} />)
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'teste' } })
    expect(handleChange).toHaveBeenCalledOnce()
  })

  it('renders placeholder text', () => {
    render(<Input placeholder="Digite aqui..." />)
    expect(screen.getByPlaceholderText('Digite aqui...')).toBeInTheDocument()
  })

  it('applies disabled attribute when disabled', () => {
    render(<Input disabled />)
    expect(screen.getByRole('textbox')).toBeDisabled()
  })
})
