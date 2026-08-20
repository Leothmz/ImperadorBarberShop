'use client'

import Link from 'next/link'
import Image from 'next/image'
import { useBarbers } from '@/hooks/useBarbers'
import { StarRatingDisplay } from '@/components/ui/StarRating'

/**
 * Os barbeiros reais da casa, com a nota real de cada um. Sem borda e sem card:
 * é uma fila de pessoas, não uma grade de produtos. Cada nome entra direto no
 * agendamento já com aquele barbeiro escolhido.
 *
 * Quem ainda não tem avaliação aparece dizendo isso — a página não fabrica nota.
 */
export function BarberLineup() {
  const { data: barbers, isLoading, isError } = useBarbers()
  const active = (barbers ?? []).filter((b) => b.isActive)

  // Sem barbeiro carregado não há seção: melhor encurtar a página que mostrar
  // um vazio decorado.
  if (isError || (!isLoading && active.length === 0)) return null

  return (
    <section className="border-t border-brand-white/10 px-4 py-20 sm:px-6">
      <div className="mx-auto max-w-5xl">
        <h2 className="font-montserrat text-3xl font-black tracking-tight text-brand-white sm:text-4xl">
          Quem vai te atender
        </h2>
        <p className="mt-3 max-w-prose text-brand-white/60">
          Escolha o profissional e vá direto para a agenda dele.
        </p>

        {isLoading ? (
          <div className="mt-10 flex flex-wrap gap-10" aria-label="Carregando barbeiros">
            {Array.from({ length: 3 }, (_, i) => (
              <div key={i} className="flex flex-col items-center gap-3">
                <span className="h-20 w-20 rounded-full bg-brand-white/10" />
                <span className="h-4 w-24 rounded bg-brand-white/10" />
              </div>
            ))}
          </div>
        ) : (
          <ul className="mt-10 flex flex-wrap gap-x-10 gap-y-8">
            {active.map((barber) => (
              <li key={barber.id}>
                <Link
                  href={`/agendar?barbeiro=${barber.id}`}
                  className="group flex flex-col items-center gap-3 rounded-xl p-2 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black"
                >
                  {barber.photoUrl ? (
                    <Image
                      src={barber.photoUrl}
                      alt=""
                      width={80}
                      height={80}
                      className="h-20 w-20 rounded-full object-cover ring-1 ring-brand-white/15 transition-[transform,box-shadow] duration-200 ease-out group-hover:-translate-y-0.5 group-hover:ring-brand-gold"
                    />
                  ) : (
                    <span
                      aria-hidden="true"
                      className="flex h-20 w-20 items-center justify-center rounded-full bg-brand-gold/20 font-montserrat text-2xl font-bold text-brand-gold ring-1 ring-brand-gold/25 transition-[transform,background-color,box-shadow] duration-200 ease-out group-hover:-translate-y-0.5 group-hover:bg-brand-gold/30 group-hover:ring-brand-gold"
                    >
                      {barber.name.charAt(0).toUpperCase()}
                    </span>
                  )}

                  <span className="font-montserrat font-semibold text-brand-white transition-colors group-hover:text-brand-gold">
                    {barber.name}
                  </span>

                  {barber.averageRating > 0 ? (
                    <span className="flex items-center gap-1.5">
                      <StarRatingDisplay rating={barber.averageRating} size="sm" />
                      <span className="text-xs text-brand-white/50">
                        {barber.averageRating.toFixed(1)}
                      </span>
                    </span>
                  ) : (
                    <span className="text-xs text-brand-white/55">Ainda sem avaliações</span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
