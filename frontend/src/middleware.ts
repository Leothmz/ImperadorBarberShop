import { NextResponse, type NextRequest } from 'next/server'

const ROLE_COOKIE = 'imperador_access_role'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  const role = request.cookies.get(ROLE_COOKIE)?.value

  if (pathname.startsWith('/admin')) {
    if (role !== 'Admin') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  if (pathname.startsWith('/barber')) {
    if (role !== 'Barber') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/barber/:path*', '/admin/:path*'],
}
