'use client'

import { useEffect, useRef, useState } from 'react'
import { useServices } from '@/hooks/useServices'
import { formatCurrency } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'

/**
 * A tabela de preços da barbearia — o quadro atrás da cadeira, não uma grade de
 * cards. Nome à esquerda, preço à direita, a régua pontilhada ligando os dois.
 * Tudo vem do catálogo real; a página não inventa serviço nem valor.
 */
export function ServiceBoard() {
  const { data: services, isLoading, isError } = useServices()
  const listRef = useRef<HTMLUListElement>(null)
  const [revealed, setRevealed] = useState(false)

  useEffect(() => {
    const node = listRef.current
    if (!node || revealed) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setRevealed(true)
          observer.disconnect()
        }
      },
      { threshold: 0.15 }
    )
    observer.observe(node)
    return () => observer.disconnect()
  }, [revealed])

  const active = (services ?? []).filter((s) => s.isActive)

  return (
    <section className="px-4 py-20 sm:px-6">
      <div className="mx-auto max-w-5xl">
        <h2 className="font-montserrat text-3xl font-black tracking-tight text-brand-white sm:text-4xl">
          O que fazemos
        </h2>
        <p className="mt-3 max-w-prose text-brand-white/60">
          Preço e duração de cada serviço. O tempo total do seu horário é a soma do que você
          escolher — por isso a agenda já mostra só os horários que cabem.
        </p>

        {isLoading && (
          <ul className="mt-10 flex max-w-3xl flex-col gap-5" aria-label="Carregando serviços">
            {Array.from({ length: 4 }, (_, i) => (
              <li key={i} className="flex items-baseline gap-4">
                <span className="h-4 w-32 rounded bg-brand-white/10" />
                <span className="h-px flex-1 bg-brand-white/10" />
                <span className="h-4 w-16 rounded bg-brand-white/10" />
              </li>
            ))}
          </ul>
        )}

        {isError && (
          <p role="alert" className="mt-10 text-brand-white/60">
            Não conseguimos carregar a tabela agora. Ela aparece inteira na hora de agendar.
          </p>
        )}

        {!isLoading && !isError && active.length > 0 && (
          <ul
            ref={listRef}
            className={['mt-10 flex max-w-3xl flex-col gap-5', revealed ? 'reveal-list' : ''].join(' ')}
          >
            {active.map((service, index) => (
              <li
                key={service.id}
                style={{ '--i': index } as React.CSSProperties}
                className="flex items-baseline gap-x-4"
              >
                <span className="font-montserrat text-lg font-semibold text-brand-white">
                  {service.name}
                </span>
                {/* A régua pontilhada só cabe quando sobra largura */}
                <span
                  aria-hidden="true"
                  className="hidden h-px min-w-8 flex-1 self-center bg-[repeating-linear-gradient(to_right,rgba(245,245,245,0.25)_0_2px,transparent_2px_6px)] sm:block"
                />
                {/* Duração e preço andam juntos: separados, o preço caía sozinho
                    para a linha de baixo em nomes longos no celular. */}
                <span className="ml-auto flex shrink-0 items-baseline gap-3 whitespace-nowrap">
                  <span className="text-sm text-brand-white/50">
                    {formatDuration(service.durationMinutes)}
                  </span>
                  <span className="font-montserrat text-lg font-bold text-brand-gold">
                    {formatCurrency(service.price)}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
