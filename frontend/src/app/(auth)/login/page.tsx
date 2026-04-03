import Link from 'next/link'
import { LoginForm } from '@/components/auth/LoginForm'
import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Entrar | O Imperador Barber Shop',
}

export default function LoginPage() {
  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center px-4 py-12">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="font-montserrat text-3xl font-black text-brand-white">
            Bem-vindo de volta
          </h1>
          <p className="mt-2 text-brand-white/60">Entre na sua conta para continuar</p>
        </div>

        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-8">
          <LoginForm />

          <p className="mt-6 text-center text-sm text-brand-white/50">
            Não tem uma conta?{' '}
            <Link
              href="/register/client"
              className="text-brand-gold hover:text-brand-gold-light transition-colors"
            >
              Cadastre-se
            </Link>
          </p>
        </div>
      </div>
    </div>
  )
}
