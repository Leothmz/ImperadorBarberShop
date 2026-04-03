'use client'

import { useState } from 'react'

interface StarRatingDisplayProps {
  rating: number
  maxStars?: number
  size?: 'sm' | 'md' | 'lg'
}

interface StarRatingInputProps {
  value: number
  onChange: (value: number) => void
  maxStars?: number
}

const sizeClass = {
  sm: 'h-4 w-4',
  md: 'h-5 w-5',
  lg: 'h-6 w-6',
}

export function StarRatingDisplay({
  rating,
  maxStars = 5,
  size = 'md',
}: StarRatingDisplayProps) {
  return (
    <div className="flex items-center gap-0.5" aria-label={`Avaliação: ${rating} de ${maxStars}`}>
      {Array.from({ length: maxStars }, (_, i) => {
        const filled = i < Math.round(rating)
        return (
          <svg
            key={i}
            className={[sizeClass[size], filled ? 'text-brand-gold' : 'text-brand-white/20'].join(
              ' '
            )}
            fill="currentColor"
            viewBox="0 0 20 20"
            aria-hidden="true"
          >
            <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
          </svg>
        )
      })}
    </div>
  )
}

export function StarRatingInput({ value, onChange, maxStars = 5 }: StarRatingInputProps) {
  const [hovered, setHovered] = useState(0)

  return (
    <div
      className="flex items-center gap-1"
      role="group"
      aria-label="Selecione uma avaliação"
    >
      {Array.from({ length: maxStars }, (_, i) => {
        const star = i + 1
        const active = star <= (hovered || value)
        return (
          <button
            key={star}
            type="button"
            onClick={() => onChange(star)}
            onMouseEnter={() => setHovered(star)}
            onMouseLeave={() => setHovered(0)}
            aria-label={`${star} estrela${star > 1 ? 's' : ''}`}
            aria-pressed={star <= value}
            className="transition-transform hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold rounded"
          >
            <svg
              className={['h-8 w-8', active ? 'text-brand-gold' : 'text-brand-white/20'].join(' ')}
              fill="currentColor"
              viewBox="0 0 20 20"
              aria-hidden="true"
            >
              <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
            </svg>
          </button>
        )
      })}
    </div>
  )
}
