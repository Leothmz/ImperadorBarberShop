'use client'

import Link from 'next/link'
import { useAuthContext } from '@/providers/AuthProvider'
import { Button, buttonClasses } from '@/components/ui/Button'
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

        {/* Só a ação do cliente mora aqui. A porta do barbeiro fica no rodapé:
            quase todo o tráfego vem do Instagram e é cliente. */}
        <nav className="flex items-center gap-3">
          <Link href="/agendar" className={buttonClasses({ size: 'md' })}>
            Agendar
          </Link>
          {user && (
            <>
              {user.role === 'Barber' && (
                <Link href="/barber/dashboard" className={buttonClasses({ variant: 'ghost', size: 'md' })}>
                  Minha Agenda
                </Link>
              )}
              <Button variant="secondary" size="md" onClick={handleLogout}>
                Sair
              </Button>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
