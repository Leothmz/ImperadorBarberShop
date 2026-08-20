'use client'

import Link from 'next/link'
import type { MouseEvent, PointerEvent, ReactNode } from 'react'
import { buttonClasses } from '@/components/ui/Button'

interface SnipButtonProps {
  href: string
  children: ReactNode
  className?: string
}

/**
 * O CTA do herói. Apertar é o corte: solta uma lufada de fios do ponto tocado,
 * e o ouro clareia a partir de onde o dedo está — a luz da sala seguindo a mão.
 */
export function SnipButton({ href, children, className = '' }: SnipButtonProps) {
  function trackPointer(event: PointerEvent<HTMLAnchorElement>) {
    const rect = event.currentTarget.getBoundingClientRect()
    event.currentTarget.style.setProperty('--mx', `${event.clientX - rect.left}px`)
    event.currentTarget.style.setProperty('--my', `${event.clientY - rect.top}px`)
  }

  function snip(event: MouseEvent<HTMLAnchorElement>) {
    // clientX é 0 quando o clique veio do teclado: aí o corte sai do centro do botão.
    const rect = event.currentTarget.getBoundingClientRect()
    const fromKeyboard = event.clientX === 0 && event.clientY === 0
    window.dispatchEvent(
      new CustomEvent('imperador:snip', {
        detail: {
          x: fromKeyboard ? rect.left + rect.width / 2 : event.clientX,
          y: fromKeyboard ? rect.top + rect.height / 2 : event.clientY,
        },
      })
    )
  }

  return (
    <Link
      href={href}
      onPointerMove={trackPointer}
      onClick={snip}
      className={buttonClasses({ size: 'lg', className: `snip-cta ${className}` })}
    >
      <span className="relative z-10">{children}</span>
    </Link>
  )
}
