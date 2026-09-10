import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import type { UserRole } from '@/types/api.types'

const replace = vi.fn()
const push = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push }),
  usePathname: () => '/admin/dashboard',
}))

interface AuthState {
  user: { userId: string; role: UserRole; barberId: string | null } | null
  isLoading: boolean
}

let authState: AuthState = { user: null, isLoading: false }

vi.mock('@/providers/AuthProvider', () => ({
  useAuthContext: () => ({ ...authState, login: vi.fn(), logout: vi.fn() }),
}))

import AdminLayout from '@/app/admin/layout'

function renderLayout() {
  return render(
    <AdminLayout>
      <p>conteúdo do admin</p>
    </AdminLayout>
  )
}

describe('AdminLayout', () => {
  beforeEach(() => {
    replace.mockClear()
    authState = { user: null, isLoading: false }
  })

  it('renders nothing of the admin shell and sends an anonymous visitor to login', () => {
    // O cenário do cookie forjado: o middleware deixou passar, mas não há sessão real
    renderLayout()

    expect(screen.queryByText('conteúdo do admin')).not.toBeInTheDocument()
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
    expect(replace).toHaveBeenCalledWith('/login')
  })

  it('sends a logged-in barber to login instead of rendering the admin area', () => {
    authState = { user: { userId: 'u1', role: 'Barber', barberId: 'b1' }, isLoading: false }

    renderLayout()

    expect(screen.queryByText('conteúdo do admin')).not.toBeInTheDocument()
    expect(replace).toHaveBeenCalledWith('/login')
  })

  it('waits for the session restore before deciding', () => {
    authState = { user: null, isLoading: true }

    renderLayout()

    expect(screen.queryByText('conteúdo do admin')).not.toBeInTheDocument()
    expect(replace).not.toHaveBeenCalled()
  })

  it('renders the admin area for an admin session', () => {
    authState = { user: { userId: 'u1', role: 'Admin', barberId: null }, isLoading: false }

    renderLayout()

    expect(screen.getByText('conteúdo do admin')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Barbeiros' })).toBeInTheDocument()
    expect(replace).not.toHaveBeenCalled()
  })
})
