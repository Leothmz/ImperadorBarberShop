'use client'

import { useState } from 'react'
import { DayPicker } from 'react-day-picker'
import { useAvailableSlots } from '@/hooks/useAvailableSlots'
import { Spinner } from '@/components/ui/Spinner'
import { formatTimeSlot, toApiDate } from '@/lib/utils/formatDateTime'
import { ptBR } from 'date-fns/locale'
import type { BarberAvailability } from '@/types/api.types'

interface SlotPickerProps {
  barberId: string
  serviceIds: string[]
  barberAvailability: BarberAvailability[]
  selectedDate: Date | null
  selectedSlot: string | null
  onDateChange: (date: Date) => void
  onSlotChange: (slot: string) => void
}

export function SlotPicker({
  barberId,
  serviceIds,
  barberAvailability,
  selectedDate,
  selectedSlot,
  onDateChange,
  onSlotChange,
}: SlotPickerProps) {
  const [month, setMonth] = useState<Date>(new Date())

  const dateStr = selectedDate ? toApiDate(selectedDate) : ''

  const {
    data: slots,
    isLoading,
    isError,
  } = useAvailableSlots({
    barberId,
    date: dateStr,
    serviceIds,
  })

  // The API returns dayOfWeek as a string enum (e.g. "Monday") because the
  // backend uses JsonStringEnumConverter. Map to JS Date.getDay() integers
  // (0=Sun, 1=Mon, …, 6=Sat) so Set.has() matches correctly.
  const DAY_NAME_TO_INDEX: Record<string, number> = {
    Sunday: 0,
    Monday: 1,
    Tuesday: 2,
    Wednesday: 3,
    Thursday: 4,
    Friday: 5,
    Saturday: 6,
  }
  const availableDays = new Set(
    barberAvailability.map((a) => DAY_NAME_TO_INDEX[a.dayOfWeek])
  )

  // Disable past dates and any day not covered by the barber's availability.
  // Do NOT hardcode Sunday (or any day) — availability is driven entirely by
  // the BarberAvailability data returned from the API.
  const today = new Date()
  today.setHours(0, 0, 0, 0)

  function isDisabled(date: Date): boolean {
    return date < today || !availableDays.has(date.getDay())
  }

  return (
    <div className="flex flex-col gap-6 lg:flex-row">
      {/* Calendar */}
      <div className="flex justify-center lg:justify-start">
        <DayPicker
          mode="single"
          selected={selectedDate ?? undefined}
          onSelect={(d) => d && onDateChange(d)}
          month={month}
          onMonthChange={setMonth}
          disabled={isDisabled}
          locale={ptBR}
          classNames={{
            root: 'rdp-root',
            months: 'flex flex-col gap-4',
            month: 'flex flex-col gap-2',
            caption: 'flex justify-between items-center px-1',
            caption_label: 'font-montserrat font-semibold text-brand-white capitalize',
            nav: 'flex gap-1',
            nav_button:
              'h-7 w-7 rounded bg-brand-white/10 text-brand-white hover:bg-brand-gold/20 transition-colors flex items-center justify-center',
            table: 'w-full border-collapse',
            head_row: 'flex',
            head_cell:
              'text-brand-white/40 rounded-md w-10 font-normal text-xs text-center',
            row: 'flex w-full mt-1',
            cell: 'relative p-0 text-center',
            day: 'h-10 w-10 rounded-full font-normal text-sm text-brand-white hover:bg-brand-gold/20 transition-colors',
            day_selected: 'bg-brand-gold text-brand-black font-bold hover:bg-brand-gold',
            day_today: 'border border-brand-gold/50 text-brand-gold',
            day_disabled: 'text-brand-white/20 cursor-not-allowed hover:bg-transparent',
            day_outside: 'text-brand-white/20',
          }}
        />
      </div>

      {/* Time slots */}
      <div className="flex-1">
        {!selectedDate ? (
          <p className="text-brand-white/50 text-sm py-4">
            Selecione uma data para ver os horários disponíveis.
          </p>
        ) : isLoading ? (
          <div className="flex justify-center py-8">
            <Spinner />
          </div>
        ) : isError ? (
          <p role="alert" className="text-red-400 text-sm">
            Erro ao carregar horários.
          </p>
        ) : !slots || slots.length === 0 ? (
          <p className="text-brand-white/50 text-sm py-4">
            Nenhum horário disponível para esta data.
          </p>
        ) : (
          <div>
            <p className="text-sm text-brand-white/60 mb-3">
              Horários disponíveis
            </p>
            <div
              className="grid grid-cols-3 gap-2 sm:grid-cols-4"
              role="listbox"
              aria-label="Horários disponíveis"
            >
              {slots.map((slot) => {
                const isSelected = slot === selectedSlot
                return (
                  <button
                    key={slot}
                    role="option"
                    aria-selected={isSelected}
                    onClick={() => onSlotChange(slot)}
                    className={[
                      'rounded-lg border py-2 px-3 text-sm font-medium transition-all duration-150 cursor-pointer',
                      isSelected
                        ? 'border-brand-gold bg-brand-gold text-brand-black'
                        : 'border-brand-white/20 text-brand-white hover:border-brand-gold/50 hover:bg-brand-gold/10',
                    ].join(' ')}
                  >
                    {formatTimeSlot(slot)}
                  </button>
                )
              })}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
