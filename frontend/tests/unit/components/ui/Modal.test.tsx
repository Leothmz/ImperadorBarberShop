import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Modal } from '@/components/ui/Modal'

describe('Modal', () => {
  it('does not render content when isOpen is false', () => {
    render(
      <Modal isOpen={false} onClose={vi.fn()} title="Teste">
        <p>Conteúdo do modal</p>
      </Modal>
    )
    expect(screen.queryByText('Conteúdo do modal')).not.toBeInTheDocument()
  })

  it('renders content when isOpen is true', () => {
    render(
      <Modal isOpen={true} onClose={vi.fn()} title="Meu Modal">
        <p>Conteúdo do modal</p>
      </Modal>
    )
    expect(screen.getByText('Conteúdo do modal')).toBeInTheDocument()
  })

  it('displays the title', () => {
    render(
      <Modal isOpen={true} onClose={vi.fn()} title="Título Especial">
        <span>conteúdo</span>
      </Modal>
    )
    expect(screen.getByText('Título Especial')).toBeInTheDocument()
  })

  it('calls onClose when the close button is clicked', () => {
    const handleClose = vi.fn()
    render(
      <Modal isOpen={true} onClose={handleClose} title="Modal">
        <span>conteúdo</span>
      </Modal>
    )
    fireEvent.click(screen.getByLabelText('Fechar modal'))
    expect(handleClose).toHaveBeenCalledOnce()
  })

  it('calls onClose when backdrop is clicked', () => {
    const handleClose = vi.fn()
    const { container } = render(
      <Modal isOpen={true} onClose={handleClose} title="Modal">
        <span>conteúdo</span>
      </Modal>
    )
    // Click the backdrop (the aria-hidden overlay div)
    const backdrop = container.querySelector('[aria-hidden="true"]') as HTMLElement
    fireEvent.click(backdrop)
    expect(handleClose).toHaveBeenCalledOnce()
  })

  it('calls onClose when Escape key is pressed', () => {
    const handleClose = vi.fn()
    render(
      <Modal isOpen={true} onClose={handleClose} title="Modal">
        <span>conteúdo</span>
      </Modal>
    )
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(handleClose).toHaveBeenCalledOnce()
  })

  it('has correct ARIA attributes for accessibility', () => {
    render(
      <Modal isOpen={true} onClose={vi.fn()} title="Modal Acessível">
        <span>conteúdo</span>
      </Modal>
    )
    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAttribute('aria-labelledby', 'modal-title')
  })
})
