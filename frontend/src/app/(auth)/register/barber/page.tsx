import Link from 'next/link'
import { BarberRegisterForm } from '@/components/auth/BarberRegisterForm'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Cadastro de Barbeiro | O Imperador Barber Shop',
}

export default function BarberRegisterPage() {
  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center px-4 py-12">
      <div className="w-full max-w-lg">
        <div className="mb-8 text-center">
          <h1 className="font-montserrat text-3xl font-black text-brand-white">
            Cadastro de Barbeiro
          </h1>
          <p className="mt-2 text-brand-white/60">
            Junte-se à equipe do Imperador
          </p>
        </div>

        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-8">
          <BarberRegisterForm />

          <p className="mt-6 text-center text-sm text-brand-white/50">
            Já tem uma conta?{' '}
            <Link
              href="/login"
              className="text-brand-gold hover:text-brand-gold-light transition-colors"
            >
              Entrar
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
