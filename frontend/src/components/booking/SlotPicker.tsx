'use client'

import { useState } from 'react'
import { DayPicker } from 'react-day-picker'
import 'react-day-picker/style.css'
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
          // Chaves da API v9 do react-day-picker. As da v8 (caption, head_row,
          // day_selected…) são ignoradas em silêncio: o dia escolhido não
          // acendia e não dava para distinguir os dias liberados.
          classNames={{
            root: 'rdp-root',
            months: 'flex flex-col gap-4',
            month: 'flex flex-col gap-2',
            month_caption: 'flex items-center justify-center py-1',
            caption_label: 'font-montserrat font-semibold text-brand-white capitalize',
            nav: 'flex items-center justify-between',
            button_previous:
              'flex h-8 w-8 items-center justify-center rounded bg-brand-white/10 fill-brand-white transition-colors hover:bg-brand-gold/20',
            button_next:
              'flex h-8 w-8 items-center justify-center rounded bg-brand-white/10 fill-brand-white transition-colors hover:bg-brand-gold/20',
            month_grid: 'w-full border-collapse',
            weekdays: 'flex',
            weekday: 'w-10 text-center text-xs font-normal text-brand-white/40',
            week: 'mt-1 flex w-full',
            day: 'h-10 w-10 p-0 text-center',
            day_button:
              'h-10 w-10 rounded-full text-sm text-brand-white transition-colors hover:bg-brand-gold/20 disabled:cursor-not-allowed disabled:text-brand-white/20 disabled:hover:bg-transparent',
            selected: '[&>button]:bg-brand-gold [&>button]:font-bold [&>button]:text-brand-black',
            today: '[&>button]:border [&>button]:border-brand-gold/50 [&>button]:text-brand-gold',
            outside: '[&>button]:text-brand-white/20',
            hidden: 'invisible',
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
                      'min-h-10 rounded-lg border py-2 px-3 text-sm font-medium transition-colors duration-150 cursor-pointer',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black',
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
