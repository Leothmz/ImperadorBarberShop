'use client'

import { useEffect, useState } from 'react'
import Image from 'next/image'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { LogoutButton } from '@/components/auth/LogoutButton'

const NAV = [
  { href: '/admin/dashboard', label: 'Dashboard' },
  { href: '/admin/barbers', label: 'Barbeiros' },
  { href: '/admin/services', label: 'Serviços' },
  { href: '/admin/whatsapp', label: 'WhatsApp' },
]

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const [open, setOpen] = useState(false)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  return (
    <div className="flex min-h-screen">
      {/* Fundo escuro atrás da gaveta — só existe no mobile, com ela aberta */}
      {open && (
        <div
          className="fixed inset-0 z-30 bg-black/60 lg:hidden"
          onClick={() => setOpen(false)}
          aria-hidden="true"
        />
      )}

      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex w-64 flex-col gap-6 border-r border-brand-white/10 bg-brand-black-soft p-6',
          'transition-transform duration-200 ease-out',
          open ? 'translate-x-0' : '-translate-x-full',
          'lg:static lg:z-auto lg:translate-x-0',
        ].join(' ')}
      >
        <div className="flex flex-col items-center gap-3">
          <Image src="/logo.png" alt="O Imperador" width={80} height={80} className="h-16 w-16" />
          <span className="font-montserrat text-sm font-semibold uppercase tracking-widest text-brand-gold">
            Administrador
          </span>
        </div>

        <nav className="flex flex-1 flex-col gap-1">
          {NAV.map(({ href, label }) => {
            const active = pathname === href
            return (
              <Link
                key={href}
                href={href}
                onClick={() => setOpen(false)}
                aria-current={active ? 'page' : undefined}
                className={[
                  'rounded-lg px-4 py-2.5 text-sm transition-colors',
                  active
                    ? 'bg-brand-gold/15 font-semibold text-brand-gold'
                    : 'text-brand-white/70 hover:bg-brand-gold/10 hover:text-brand-gold',
                ].join(' ')}
              >
                {label}
              </Link>
            )
          })}
        </nav>

        <LogoutButton />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        {/* Barra com o botão da gaveta — desnecessária a partir de lg, onde a sidebar é fixa */}
        <div className="sticky top-0 z-20 flex items-center gap-3 border-b border-brand-white/10 bg-brand-black/95 px-4 py-3 backdrop-blur lg:hidden">
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            aria-expanded={open}
            aria-label={open ? 'Fechar menu' : 'Abrir menu'}
            className="rounded-lg border border-brand-white/15 px-3 py-1.5 text-brand-gold transition-colors hover:bg-brand-gold/10"
          >
            <span aria-hidden="true" className="text-lg leading-none">
              {open ? '✕' : '☰'}
            </span>
          </button>
          <span className="font-montserrat text-sm font-semibold uppercase tracking-widest text-brand-white/70">
            Administrador
          </span>
        </div>

        <main className="min-w-0 flex-1 p-4 sm:p-6 lg:p-8">{children}</main>
      </div>
    </div>
  )
}
