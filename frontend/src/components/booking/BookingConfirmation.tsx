'use client'

import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { formatCurrency, formatDateTime, toApiDate } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'
import { isValidBrPhone } from '@/lib/utils/phone'
import type { Barber, Service } from '@/types/api.types'

interface BookingConfirmationProps {
  barber: Barber
  services: Service[]
  selectedDate: Date
  selectedSlot: string
  notes: string
  onNotesChange: (notes: string) => void
  clientName: string
  onClientNameChange: (name: string) => void
  clientPhone: string
  onClientPhoneChange: (phone: string) => void
  onConfirm: () => void
  isLoading: boolean
}

export function BookingConfirmation({
  barber,
  services,
  selectedDate,
  selectedSlot,
  notes,
  onNotesChange,
  clientName,
  onClientNameChange,
  clientPhone,
  onClientPhoneChange,
  onConfirm,
  isLoading,
}: BookingConfirmationProps) {
  const totalDuration = services.reduce((acc, s) => acc + s.durationMinutes, 0)
  const totalPrice = services.reduce((acc, s) => acc + s.price, 0)

  // Build UTC ISO datetime the same way book/page.tsx does before sending to
  // the API — append "Z" so the display matches the value that will be stored.
  const dateString = toApiDate(selectedDate) // "YYYY-MM-DD"
  const scheduledAt = new Date(`${dateString}T${selectedSlot}Z`)

  const canConfirm = clientName.trim().length > 0 && isValidBrPhone(clientPhone)

  return (
    <div className="flex flex-col gap-6">
      {/* Contact info */}
      <div className="flex flex-col gap-3">
        <Input
          label="Nome completo"
          value={clientName}
          onChange={(e) => onClientNameChange(e.target.value)}
          placeholder="Seu nome"
        />
        <Input
          label="WhatsApp"
          value={clientPhone}
          onChange={(e) => onClientPhoneChange(e.target.value)}
          placeholder="+55 11 99999-0000"
        />
      </div>

      {/* Summary card */}
      <div className="rounded-xl border border-brand-gold/30 bg-brand-black-soft p-6">
        <h3 className="font-montserrat font-bold text-brand-white mb-4">
          Resumo do agendamento
        </h3>

        <div className="flex flex-col gap-3">
          <div className="flex justify-between">
            <span className="text-brand-white/60 text-sm">Barbeiro</span>
            <span className="text-brand-white font-medium">{barber.name}</span>
          </div>

          <div className="border-t border-brand-white/10 pt-3">
            <span className="text-brand-white/60 text-sm block mb-2">Serviços</span>
            {services.map((s) => (
              <div key={s.id} className="flex justify-between text-sm py-1">
                <span className="text-brand-white">{s.name}</span>
                <span className="text-brand-gold">{formatCurrency(s.price)}</span>
              </div>
            ))}
          </div>

          <div className="border-t border-brand-white/10 pt-3 flex justify-between">
            <span className="text-brand-white/60 text-sm">Duração total</span>
            <span className="text-brand-white font-medium">{formatDuration(totalDuration)}</span>
          </div>

          <div className="flex justify-between">
            <span className="text-brand-white/60 text-sm">Data e horário</span>
            <span className="text-brand-white font-medium">
              {formatDateTime(scheduledAt.toISOString())}
            </span>
          </div>

          <div className="border-t border-brand-gold/30 pt-3 flex justify-between">
            <span className="font-montserrat font-semibold text-brand-white">Total</span>
            <span className="font-montserrat font-bold text-brand-gold text-xl">
              {formatCurrency(totalPrice)}
            </span>
          </div>
        </div>
      </div>

      {/* Notes */}
      <div className="flex flex-col gap-1">
        <label
          htmlFor="booking-notes"
          className="text-sm font-medium text-brand-white/80"
        >
          Observações (opcional)
        </label>
        <textarea
          id="booking-notes"
          value={notes}
          onChange={(e) => onNotesChange(e.target.value)}
          placeholder="Ex: Prefiro o corte mais curto nas laterais..."
          rows={3}
          className="w-full rounded-md border border-brand-white/20 bg-brand-black-soft px-3 py-2.5 text-brand-white placeholder:text-brand-white/30 focus:border-brand-gold focus:outline-none focus:ring-1 focus:ring-brand-gold resize-none"
        />
      </div>

      <Button
        onClick={onConfirm}
        isLoading={isLoading}
        disabled={!canConfirm}
        size="lg"
        className="w-full"
      >
        Confirmar Agendamento
      </Button>
    </div>
  )
}
