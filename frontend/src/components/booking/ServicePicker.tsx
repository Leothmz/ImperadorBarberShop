'use client'

import { useServices } from '@/hooks/useServices'
import { Spinner } from '@/components/ui/Spinner'
import { formatDuration } from '@/lib/utils/formatDuration'
import { formatCurrency } from '@/lib/utils/formatDateTime'
import type { Service } from '@/types/api.types'

interface ServicePickerProps {
  selectedServiceIds: string[]
  onToggle: (service: Service) => void
}

export function ServicePicker({ selectedServiceIds, onToggle }: ServicePickerProps) {
  const { data: services, isLoading, isError } = useServices()

  const selectedServices = services?.filter((s) => selectedServiceIds.includes(s.id)) ?? []
  const totalDuration = selectedServices.reduce((acc, s) => acc + s.durationMinutes, 0)
  const totalPrice = selectedServices.reduce((acc, s) => acc + s.price, 0)

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
        Erro ao carregar serviços. Tente novamente.
      </p>
    )
  }

  if (!services || services.length === 0) {
    return (
      <p className="text-center text-brand-white/50 py-8">
        Nenhum serviço disponível no momento.
      </p>
    )
  }

  const activeServices = services.filter((s) => s.isActive)

  return (
    <div className="flex flex-col gap-4">
      <div
        className="flex flex-col gap-3"
        role="group"
        aria-label="Selecione os serviços"
      >
        {activeServices.map((service) => {
          const isSelected = selectedServiceIds.includes(service.id)
          return (
            <label
              key={service.id}
              className={[
                'flex cursor-pointer items-center gap-4 rounded-xl border p-4 transition-all duration-150',
                isSelected
                  ? 'border-brand-gold bg-brand-gold/10'
                  : 'border-brand-white/10 bg-brand-black-soft hover:border-brand-gold/50',
              ].join(' ')}
            >
              <input
                type="checkbox"
                checked={isSelected}
                onChange={() => onToggle(service)}
                className="h-5 w-5 rounded accent-brand-gold"
                aria-label={`Selecionar ${service.name}`}
              />
              <div className="flex-1">
                <p className="font-medium text-brand-white">{service.name}</p>
                {service.description && (
                  <p className="text-sm text-brand-white/50">{service.description}</p>
                )}
              </div>
              <div className="text-right shrink-0">
                <p className="font-semibold text-brand-gold">{formatCurrency(service.price)}</p>
                <p className="text-xs text-brand-white/50">{formatDuration(service.durationMinutes)}</p>
              </div>
            </label>
          )
        })}
      </div>

      {/* Running total */}
      {selectedServiceIds.length > 0 && (
        <div
          className="sticky bottom-0 flex items-center justify-between rounded-xl border border-brand-gold/30 bg-brand-black-soft p-4"
          aria-live="polite"
          aria-label="Total dos serviços selecionados"
        >
          <span className="text-sm text-brand-white/70">
            {selectedServiceIds.length} serviço{selectedServiceIds.length !== 1 ? 's' : ''} •{' '}
            {formatDuration(totalDuration)}
          </span>
          <span className="font-montserrat font-bold text-brand-gold text-lg">
            {formatCurrency(totalPrice)}
          </span>
        </div>
      )}
    </div>
  )
}
