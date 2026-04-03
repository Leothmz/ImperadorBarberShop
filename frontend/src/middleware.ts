import { NextResponse, type NextRequest } from 'next/server'

const ROLE_COOKIE = 'imperador_access_role'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl
  const role = request.cookies.get(ROLE_COOKIE)?.value

  // Protect client routes
  if (pathname.startsWith('/client')) {
    if (!role || role !== 'Client') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  // Protect barber routes
  if (pathname.startsWith('/barber')) {
    if (!role || role !== 'Barber') {
      const loginUrl = new URL('/login', request.url)
      loginUrl.searchParams.set('redirect', pathname)
      return NextResponse.redirect(loginUrl)
    }
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/client/:path*', '/barber/:path*'],
}
