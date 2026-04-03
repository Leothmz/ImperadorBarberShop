import Link from 'next/link'
import { ClientRegisterForm } from '@/components/auth/ClientRegisterForm'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Cadastro de Cliente | O Imperador Barber Shop',
}

export default function ClientRegisterPage() {
  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="font-montserrat text-3xl font-black text-brand-white">
            Crie sua conta
          </h1>
          <p className="mt-2 text-brand-white/60">
            Agende seus cortes com facilidade
          </p>
        </div>

        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-8">
          <ClientRegisterForm />

          <div className="mt-6 flex flex-col items-center gap-2 text-sm text-brand-white/50">
            <p>
              Já tem uma conta?{' '}
              <Link
                href="/login"
                className="text-brand-gold hover:text-brand-gold-light transition-colors"
              >
                Entrar
              </Link>
            </p>
            <p>
              É barbeiro?{' '}
              <Link
                href="/register/barber"
                className="text-brand-gold hover:text-brand-gold-light transition-colors"
              >
                Cadastre-se como barbeiro
              </Link>
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
