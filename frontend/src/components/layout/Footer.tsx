import Link from 'next/link'

export function Footer() {
  return (
    <footer className="border-t border-brand-white/10 bg-brand-black py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <div className="flex flex-col items-center gap-4 sm:flex-row sm:justify-between">
          <div className="flex flex-col items-center sm:items-start">
            <span className="font-montserrat text-lg font-black tracking-widest text-brand-gold">
              O IMPERADOR
            </span>
            <span className="font-montserrat text-[0.5rem] tracking-[0.35em] text-brand-gold/50">
              BARBER SHOP
            </span>
          </div>
          <div className="flex flex-col items-center gap-2 sm:items-end">
            {/* A entrada da equipe vive aqui, fora do caminho do cliente */}
            <Link
              href="/login"
              className="rounded px-2 py-2 text-sm text-brand-white/60 transition-colors hover:text-brand-gold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black"
            >
              Área do barbeiro
            </Link>
            <p className="text-sm text-brand-white/60">
              &copy; {new Date().getFullYear()} O Imperador Barber Shop. Todos os direitos reservados.
            </p>
          </div>
        </div>
      </div>
    </footer>
  )
}
