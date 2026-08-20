import Link from 'next/link'
import Image from 'next/image'
import { buttonClasses } from '@/components/ui/Button'
import { HairRain } from '@/components/landing/HairRain'
import { SnipButton } from '@/components/landing/SnipButton'
import { HowItWorks } from '@/components/landing/HowItWorks'
import { ServiceBoard } from '@/components/landing/ServiceBoard'
import { BarberLineup } from '@/components/landing/BarberLineup'

export default function LandingPage() {
  return (
    <>
      {/* Hero Section */}
      {/* svh, não vh: no celular o vh mede a viewport maior e empurra o CTA para
          fora da tela na primeira pintura. */}
      <section className="relative flex min-h-[85svh] flex-col items-center justify-center overflow-hidden px-4 py-10 text-center">
        {/* A luz única da sala */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(ellipse 80% 60% at 50% 0%, rgba(201,168,76,0.12) 0%, transparent 70%)',
          }}
        />

        {/* O instante depois de a máquina passar */}
        <HairRain />

        <div className="relative z-10 flex flex-col items-center gap-4 max-w-3xl sm:gap-6">
          <Image
            src="/logo.png"
            alt="O Imperador Barber Shop"
            width={160}
            height={160}
            className="h-28 w-28 sm:mb-2 sm:h-40 sm:w-40"
            priority
          />

          {/* clamp e nowrap: no celular o "O" quebrava sozinho numa linha e
              virava um ponto branco solto acima do nome. */}
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
            <SnipButton href="/agendar" className="min-w-[220px]">
              Agendar agora
            </SnipButton>
          </div>
        </div>
      </section>

      <HowItWorks />
      <ServiceBoard />
      <BarberLineup />

      {/* CTA Section */}
      <section className="border-t border-brand-white/10 px-4 py-20">
        <div className="mx-auto max-w-3xl text-center">
          <h2 className="font-montserrat text-3xl font-black tracking-tight text-brand-white sm:text-4xl">
            Sua cadeira está livre
          </h2>
          <p className="mx-auto mt-3 max-w-prose text-lg text-brand-white/60">
            Leva menos de um minuto e não pede cadastro.
          </p>
          <div className="mt-8">
            <Link href="/agendar" className={buttonClasses({ size: 'lg' })}>
              Agendar agora
            </Link>
          </div>
        </div>
      </section>
    </>
  )
}
