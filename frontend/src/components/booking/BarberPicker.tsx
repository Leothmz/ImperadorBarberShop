'use client'

import { useBarbers } from '@/hooks/useBarbers'
import { StarRatingDisplay } from '@/components/ui/StarRating'
import { Spinner } from '@/components/ui/Spinner'
import type { Barber } from '@/types/api.types'

interface BarberPickerProps {
  selectedBarberId: string | null
  onSelect: (barber: Barber) => void
}

export function BarberPicker({ selectedBarberId, onSelect }: BarberPickerProps) {
  const { data: barbers, isLoading, isError } = useBarbers()

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError) {
    return (
      <p role="alert" className="text-center text-red-400 py-8">
        Erro ao carregar barbeiros. Tente novamente.
      </p>
    )
  }

  if (!barbers || barbers.length === 0) {
    return (
      <p className="text-center text-brand-white/50 py-8">
        Nenhum barbeiro disponível no momento.
      </p>
    )
  }

  return (
    <ul
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
      aria-label="Lista de barbeiros"
    >
      {barbers.map((barber) => {
        const isSelected = barber.id === selectedBarberId
        return (
          <li key={barber.id} className="flex">
          <button
            type="button"
            onClick={() => onSelect(barber)}
            aria-pressed={isSelected}
            className={[
              'flex w-full flex-col items-center gap-3 rounded-xl border p-6 text-center transition-colors duration-150 cursor-pointer',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black',
              isSelected
                ? 'border-brand-gold bg-brand-gold/10'
                : 'border-brand-white/10 bg-brand-black-soft hover:border-brand-gold/50 hover:bg-brand-gold/5',
            ].join(' ')}
          >
            {/* Avatar placeholder */}
            <div className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-gold/20 text-2xl font-bold text-brand-gold font-montserrat">
              {barber.name.charAt(0).toUpperCase()}
            </div>

            <div>
              <p className="font-montserrat font-semibold text-brand-white">{barber.name}</p>
              <div className="mt-1 flex items-center justify-center gap-1">
                <StarRatingDisplay rating={barber.averageRating} size="sm" />
                <span className="text-xs text-brand-white/50">
                  {barber.averageRating > 0
                    ? barber.averageRating.toFixed(1)
                    : 'Sem avaliações'}
                </span>
              </div>
            </div>

            {isSelected && (
              <span className="text-xs font-semibold text-brand-gold">Selecionado</span>
            )}
          </button>
          </li>
        )
      })}
    </ul>
  )
}
