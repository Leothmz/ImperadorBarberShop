import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { server } from '../../mocks/server'
import { mockBarberLoginResult } from '../../mocks/handlers'
import { AuthProvider, useAuthContext } from '@/providers/AuthProvider'

function Probe() {
  const { user, isLoading, login, logout } = useAuthContext()
  return (
    <div>
      <p data-testid="state">{isLoading ? 'loading' : (user?.role ?? 'anonymous')}</p>
      <button onClick={() => login(mockBarberLoginResult)}>login</button>
      <button onClick={logout}>logout</button>
    </div>
  )
}

function renderProvider() {
  return render(
    <AuthProvider>
      <Probe />
    </AuthProvider>
  )
}

function clearCookies() {
  for (const cookie of document.cookie.split(';')) {
    const name = cookie.split('=')[0]?.trim()
    if (name) document.cookie = `${name}=;path=/;max-age=0`
  }
}

describe('AuthProvider', () => {
  beforeEach(() => {
    localStorage.clear()
    clearCookies()
  })

  it('does not write a role cookie or store a refresh token on login', async () => {
    renderProvider()
    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous'))

    fireEvent.click(screen.getByRole('button', { name: 'login' }))

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('Barber'))
    // O papel vinha num cookie escrito aqui, que o middleware aceitava sem conferir
    expect(document.cookie).not.toContain('imperador_access_role')
    expect(localStorage.getItem('imperador_user_id')).toBe(mockBarberLoginResult.userId)
    expect(localStorage.getItem('imperador_refresh_token')).toBeNull()
  })

  it('restores the session sending only the userId — the refresh token stays in the HttpOnly cookie', async () => {
    localStorage.setItem('imperador_user_id', 'user-barber-1')
    localStorage.setItem('imperador_refresh_token', 'legacy-token-from-before')
    let refreshBody: unknown = null
    server.use(
      http.post('*/api/v1/auth/refresh', async ({ request }) => {
        refreshBody = await request.json()
        return HttpResponse.json(mockBarberLoginResult)
      })
    )

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('Barber'))
    expect(refreshBody).toEqual({ userId: 'user-barber-1' })
    // A cópia antiga do refresh token não fica exposta no localStorage
    expect(localStorage.getItem('imperador_refresh_token')).toBeNull()
  })

  it('does not call refresh for an anonymous visitor', async () => {
    let refreshCalled = false
    server.use(
      http.post('*/api/v1/auth/refresh', () => {
        refreshCalled = true
        return HttpResponse.json(mockBarberLoginResult)
      })
    )

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous'))
    expect(refreshCalled).toBe(false)
  })

  it('clears the stored session when the API refuses the refresh', async () => {
    localStorage.setItem('imperador_user_id', 'user-barber-1')
    server.use(
      http.post('*/api/v1/auth/refresh', () => new HttpResponse(null, { status: 401 }))
    )

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('anonymous'))
    expect(localStorage.getItem('imperador_user_id')).toBeNull()
  })

  it('asks the API to clear the HttpOnly cookie on logout', async () => {
    let logoutCalled = false
    server.use(
      http.post('*/api/v1/auth/logout', () => {
        logoutCalled = true
        return new HttpResponse(null, { status: 204 })
      })
    )
    renderProvider()
    fireEvent.click(screen.getByRole('button', { name: 'login' }))
    await waitFor(() => expect(screen.getByTestId('state')).toHaveTextContent('Barber'))

    fireEvent.click(screen.getByRole('button', { name: 'logout' }))

    await waitFor(() => expect(logoutCalled).toBe(true))
    expect(screen.getByTestId('state')).toHaveTextContent('anonymous')
    expect(localStorage.getItem('imperador_user_id')).toBeNull()
  })
})
