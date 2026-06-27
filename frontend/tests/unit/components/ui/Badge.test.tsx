import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Badge } from '@/components/ui/Badge'
import type { AppointmentStatus } from '@/types/api.types'

describe('Badge', () => {
  const statuses: AppointmentStatus[] = ['Accepted', 'Cancelled', 'Completed']

  it('renders without crashing for all statuses', () => {
    statuses.forEach((status) => {
      const { unmount } = render(<Badge status={status} />)
      unmount()
    })
  })

  it('displays "Confirmado" for Accepted status', () => {
    render(<Badge status="Accepted" />)
    expect(screen.getByText('Confirmado')).toBeInTheDocument()
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
    render(<Badge status="Accepted" className="test-class" />)
    const badge = screen.getByText('Confirmado')
    expect(badge.className).toContain('test-class')
  })
})
