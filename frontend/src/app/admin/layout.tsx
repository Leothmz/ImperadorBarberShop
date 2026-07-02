import Image from 'next/image'
import Link from 'next/link'
import { LogoutButton } from '@/components/auth/LogoutButton'

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen">
      {/* Sidebar */}
      <aside className="w-64 bg-brand-black-soft border-r border-brand-white/10 flex flex-col p-6 gap-6">
        <div className="flex flex-col items-center gap-3">
          <Image src="/logo.svg" alt="O Imperador" width={80} height={80} />
          <span className="font-montserrat text-sm font-semibold text-brand-gold uppercase tracking-widest">
            Administrador
          </span>
        </div>

        <nav className="flex flex-col gap-2 flex-1">
          {[
            { href: '/admin/dashboard', label: 'Dashboard' },
            { href: '/admin/barbers', label: 'Barbeiros' },
            { href: '/admin/services', label: 'Serviços' },
          ].map(({ href, label }) => (
            <Link
              key={href}
              href={href}
              className="rounded-lg px-4 py-2 text-sm text-brand-white/70 hover:bg-brand-gold/10 hover:text-brand-gold transition-colors"
            >
              {label}
            </Link>
          ))}
        </nav>

        <LogoutButton />
      </aside>

      <main className="flex-1 p-8">{children}</main>
    </div>
  )
}
