import Link from 'next/link'
import { Button } from '@/components/ui/Button'

export default function LandingPage() {
  return (
    <>
      {/* Hero Section */}
      <section className="relative flex min-h-[85vh] flex-col items-center justify-center overflow-hidden px-4 text-center">
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

        <div className="relative z-10 flex flex-col items-center gap-6 max-w-3xl">
          {/* Badge */}
          <span className="inline-flex items-center gap-2 rounded-full border border-brand-gold/30 bg-brand-gold/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-brand-gold">
            ✦ Excelência em cada corte
          </span>

          {/* Main heading */}
          <h1 className="font-montserrat text-5xl font-black leading-tight tracking-tight text-brand-white sm:text-7xl">
            O{' '}
            <span className="text-brand-gold">IMPERADOR</span>
          </h1>
          <p className="font-montserrat text-lg font-light tracking-[0.4em] text-brand-white/60 uppercase -mt-4">
            BARBER SHOP
          </p>

          <p className="max-w-xl text-lg text-brand-white/60 leading-relaxed">
            Experimente o melhor da barbearia tradicional com um toque de sofisticação.
            Agende seu horário com os melhores profissionais da cidade.
          </p>

          <div className="flex flex-col gap-3 sm:flex-row mt-2">
            <Link href="/client/book">
              <Button size="lg" className="min-w-[200px]">
                Agendar agora
              </Button>
            </Link>
            <Link href="/register/client">
              <Button variant="secondary" size="lg" className="min-w-[200px]">
                Criar conta
              </Button>
            </Link>
          </div>
        </div>

        {/* Scroll indicator */}
        <div
          className="absolute bottom-8 flex flex-col items-center gap-2"
          aria-hidden="true"
        >
          <span className="text-xs tracking-widest text-brand-white/30 uppercase">
            Conheça nossos barbeiros
          </span>
          <div className="h-6 w-px bg-gradient-to-b from-brand-gold/50 to-transparent" />
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 px-4">
        <div className="mx-auto max-w-7xl">
          <div className="grid grid-cols-1 gap-8 sm:grid-cols-3">
            {[
              {
                icon: '✂',
                title: 'Barbeiros Experientes',
                description:
                  'Profissionais com anos de experiência prontos para transformar seu visual.',
              },
              {
                icon: '📅',
                title: 'Agendamento Fácil',
                description:
                  'Escolha data, horário e serviços em poucos cliques, sem espera.',
              },
              {
                icon: '⭐',
                title: 'Qualidade Garantida',
                description:
                  'Avaliações reais de clientes para que você escolha com confiança.',
              },
            ].map((feat) => (
              <div
                key={feat.title}
                className="flex flex-col items-center gap-4 rounded-xl border border-brand-white/10 bg-brand-black-soft p-8 text-center transition-colors hover:border-brand-gold/30"
              >
                <span
                  className="text-4xl"
                  aria-hidden="true"
                >
                  {feat.icon}
                </span>
                <h3 className="font-montserrat font-bold text-brand-white">
                  {feat.title}
                </h3>
                <p className="text-sm text-brand-white/60 leading-relaxed">
                  {feat.description}
                </p>
              </div>
            ))}
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
            Cadastre-se gratuitamente e agende seu primeiro corte hoje mesmo.
          </p>
          <div className="flex flex-col gap-3 sm:flex-row justify-center">
            <Link href="/register/client">
              <Button size="lg">Criar conta de cliente</Button>
            </Link>
            <Link href="/register/barber">
              <Button variant="secondary" size="lg">
                Sou barbeiro
              </Button>
            </Link>
          </div>
        </div>
      </section>
    </>
  )
}
