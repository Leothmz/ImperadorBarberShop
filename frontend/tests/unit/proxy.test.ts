// @vitest-environment node
import { describe, it, expect } from 'vitest'
import { NextRequest } from 'next/server'
import { proxy } from '@/proxy'

function requestTo(path: string, cookie?: string) {
  return new NextRequest(`http://localhost:3000${path}`, {
    headers: cookie ? { cookie } : undefined,
  })
}

function redirectTarget(response: Response) {
  const location = response.headers.get('location')
  return location ? new URL(location) : null
}

describe('proxy', () => {
  it('does not let a forged role cookie into /admin', () => {
    // Antes bastava isto: o cookie de papel era escrito pelo próprio JavaScript da página
    const response = proxy(requestTo('/admin/dashboard', 'imperador_access_role=Admin'))

    expect(response.status).toBe(307)
    const target = redirectTarget(response)
    expect(target?.pathname).toBe('/login')
    expect(target?.searchParams.get('redirect')).toBe('/admin/dashboard')
  })

  it('does not let a forged role cookie into /barber', () => {
    const response = proxy(requestTo('/barber/dashboard', 'imperador_access_role=Barber'))

    expect(response.status).toBe(307)
    expect(redirectTarget(response)?.pathname).toBe('/login')
  })

  it('redirects to login when there is no session cookie at all', () => {
    const response = proxy(requestTo('/admin/barbers'))

    expect(response.status).toBe(307)
    expect(redirectTarget(response)?.searchParams.get('redirect')).toBe('/admin/barbers')
  })

  it('lets the request through when the API-issued session cookie is present', () => {
    const response = proxy(requestTo('/admin/dashboard', 'imperador_refresh_token=opaque'))

    expect(response.headers.get('location')).toBeNull()
    expect(response.headers.get('x-middleware-next')).toBe('1')
  })
})
