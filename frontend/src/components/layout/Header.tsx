'use client'

import Link from 'next/link'
import { useAuthContext } from '@/providers/AuthProvider'
import { Button } from '@/components/ui/Button'
import { useRouter } from 'next/navigation'

export function Header() {
  const { user, logout } = useAuthContext()
  const router = useRouter()

  function handleLogout() {
    logout()
    router.push('/')
  }

  return (
    <header className="sticky top-0 z-40 border-b border-brand-white/10 bg-brand-black/95 backdrop-blur-sm">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6">
        <Link href="/" className="flex flex-col leading-none group">
          <span className="font-montserrat text-xl font-black tracking-widest text-brand-gold group-hover:text-brand-gold-light transition-colors">
            O IMPERADOR
          </span>
          <span className="font-montserrat text-[0.55rem] tracking-[0.35em] text-brand-gold/60 group-hover:text-brand-gold/80 transition-colors">
            BARBER SHOP
          </span>
        </Link>

        <nav className="flex items-center gap-3">
          <Link href="/agendar">
            <Button variant="primary" size="sm">
              Agendar
            </Button>
          </Link>
          {!user ? (
            <Link href="/login">
              <Button variant="ghost" size="sm">
                Entrar
              </Button>
            </Link>
          ) : (
            <>
              {user.role === 'Barber' && (
                <Link href="/barber/dashboard">
                  <Button variant="ghost" size="sm">
                    Minha Agenda
                  </Button>
                </Link>
              )}
              <Button variant="secondary" size="sm" onClick={handleLogout}>
                Sair
              </Button>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
