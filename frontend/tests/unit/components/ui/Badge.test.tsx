import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Badge } from '@/components/ui/Badge'
import type { AppointmentStatus } from '@/types/api.types'

describe('Badge', () => {
  const statuses: AppointmentStatus[] = ['Pending', 'Accepted', 'Rejected', 'Cancelled', 'Completed']

  it('renders without crashing for all statuses', () => {
    statuses.forEach((status) => {
      const { unmount } = render(<Badge status={status} />)
      unmount()
    })
  })

  it('displays "Pendente" for Pending status', () => {
    render(<Badge status="Pending" />)
    expect(screen.getByText('Pendente')).toBeInTheDocument()
  })

  it('displays "Aceito" for Accepted status', () => {
    render(<Badge status="Accepted" />)
    expect(screen.getByText('Aceito')).toBeInTheDocument()
  })

  it('displays "Recusado" for Rejected status', () => {
    render(<Badge status="Rejected" />)
    expect(screen.getByText('Recusado')).toBeInTheDocument()
  })

  it('displays "Cancelado" for Cancelled status', () => {
    render(<Badge status="Cancelled" />)
    expect(screen.getByText('Cancelado')).toBeInTheDocument()
  })

  it('displays "Concluído" for Completed status', () => {
    render(<Badge status="Completed" />)
    expect(screen.getByText('Concluído')).toBeInTheDocument()
  })

  it('applies custom className', () => {
    render(<Badge status="Pending" className="test-class" />)
    const badge = screen.getByText('Pendente')
    expect(badge.className).toContain('test-class')
  })
})
