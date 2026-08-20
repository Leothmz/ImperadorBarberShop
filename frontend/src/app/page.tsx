import Link from 'next/link'
import Image from 'next/image'
import { buttonClasses } from '@/components/ui/Button'

export default function LandingPage() {
  return (
    <>
      {/* Hero Section */}
      {/* svh, não vh: no celular o vh mede a viewport maior e empurra o CTA para
          fora da tela na primeira pintura. */}
      <section className="relative flex min-h-[85svh] flex-col items-center justify-center overflow-hidden px-4 py-10 text-center">
        {/* Background gradient decoration */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(ellipse 80% 60% at 50% 0%, rgba(201,168,76,0.12) 0%, transparent 70%)',
          }}
        />

        {/* Decorative lines */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 h-[600px] w-[600px] rounded-full border border-brand-gold/5"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 h-[400px] w-[400px] rounded-full border border-brand-gold/8"
        />

        <div className="relative z-10 flex flex-col items-center gap-4 max-w-3xl sm:gap-6">
          {/* Logo */}
          <Image
            src="/logo.png"
            alt="O Imperador Barber Shop"
            width={160}
            height={160}
            className="h-28 w-28 sm:mb-2 sm:h-40 sm:w-40"
            priority
          />

          {/* O diferencial real do produto, dito antes de qualquer outra coisa */}
          <span className="inline-flex items-center rounded-full border border-brand-gold/30 bg-brand-gold/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-brand-gold">
            Agendamento sem cadastro
          </span>

          {/* Main heading — clamp e nowrap: no celular o "O" quebrava sozinho
              numa linha e virava um ponto branco solto acima do nome. */}
          <h1 className="font-montserrat text-[clamp(2.25rem,11vw,4.5rem)] font-black leading-tight tracking-tight whitespace-nowrap text-brand-white">
            O <span className="text-brand-gold">IMPERADOR</span>
          </h1>
          <p className="font-montserrat text-lg font-light tracking-[0.4em] text-brand-white/60 uppercase -mt-4">
            BARBER SHOP
          </p>

          <p className="max-w-xl text-lg text-brand-white/60 leading-relaxed">
            Escolha o barbeiro, os serviços e o horário. Sem criar conta, sem esperar resposta —
            você recebe um link para acompanhar ou cancelar quando precisar.
          </p>

          <div className="mt-2">
            <Link href="/agendar" className={buttonClasses({ size: 'lg', className: 'min-w-[220px]' })}>
              Agendar agora
            </Link>
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 px-4">
        <div className="mx-auto max-w-3xl rounded-2xl border border-brand-gold/20 bg-brand-black-soft p-12 text-center">
          <h2 className="font-montserrat text-3xl font-black text-brand-white mb-4">
            Pronto para uma nova experiência?
          </h2>
          <p className="text-brand-white/60 mb-8 text-lg">
            Agende seu primeiro corte hoje mesmo, sem cadastro.
          </p>
          <Link href="/agendar" className={buttonClasses({ size: 'lg' })}>
            Agendar agora
          </Link>
        </div>
      </section>
    </>
  )
}
