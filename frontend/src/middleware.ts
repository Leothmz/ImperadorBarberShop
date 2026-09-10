import { NextResponse, type NextRequest } from 'next/server'

// Cookie HttpOnly com o refresh token, emitido pela API no login (AuthController.
// RefreshCookieName). O JavaScript da página não o lê nem o escreve — ao contrário do
// antigo cookie de papel, que qualquer um forjava com "imperador_access_role=Admin".
const SESSION_COOKIE = 'imperador_refresh_token'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl

  // Primeira linha: sem sessão emitida pela API, a área restrita nem renderiza. O
  // cookie é opaco, então o papel (Admin x Barber) é conferido na segunda linha — os
  // layouts de /admin e /barber, contra o que a própria API devolve no refresh.
  if (!request.cookies.has(SESSION_COOKIE)) {
    const loginUrl = new URL('/login', request.url)
    loginUrl.searchParams.set('redirect', pathname)
    return NextResponse.redirect(loginUrl)
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/barber/:path*', '/admin/:path*'],
}
