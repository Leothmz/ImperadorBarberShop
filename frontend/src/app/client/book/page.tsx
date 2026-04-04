'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { BarberPicker } from '@/components/booking/BarberPicker'
import { ServicePicker } from '@/components/booking/ServicePicker'
import { SlotPicker } from '@/components/booking/SlotPicker'
import { BookingConfirmation } from '@/components/booking/BookingConfirmation'
import { Button } from '@/components/ui/Button'
import { useCreateAppointment } from '@/hooks/useAppointments'
import { useServices } from '@/hooks/useServices'
import { toApiDate } from '@/lib/utils/formatDateTime'
import type { Barber, Service } from '@/types/api.types'

type Step = 1 | 2 | 3 | 4

const STEP_LABELS = [
  'Barbeiro',
  'Serviços',
  'Data e Horário',
  'Confirmar',
]

export default function BookPage() {
  const router = useRouter()
  const [step, setStep] = useState<Step>(1)

  const [selectedBarber, setSelectedBarber] = useState<Barber | null>(null)
  const [selectedServiceIds, setSelectedServiceIds] = useState<string[]>([])
  const [selectedDate, setSelectedDate] = useState<Date | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null)
  const [notes, setNotes] = useState('')

  const { data: allServices } = useServices()
  const createAppointment = useCreateAppointment()

  function toggleService(service: Service) {
    setSelectedServiceIds((prev) =>
      prev.includes(service.id)
        ? prev.filter((id) => id !== service.id)
        : [...prev, service.id]
    )
  }

  function canAdvance(): boolean {
    if (step === 1) return !!selectedBarber
    if (step === 2) return selectedServiceIds.length > 0
    if (step === 3) return !!selectedDate && !!selectedSlot
    return true
  }

  function handleNext() {
    if (step < 4) setStep((step + 1) as Step)
  }

  function handleBack() {
    if (step > 1) setStep((step - 1) as Step)
  }

  async function handleConfirm() {
    if (!selectedBarber || !selectedDate || !selectedSlot) return

    // Append "Z" so the Date constructor treats the value as UTC, not local
    // time. Without "Z", a client in UTC-3 booking a 10:00 slot would send
    // 13:00:00Z to the backend — 3 hours off.
    const dateString = toApiDate(selectedDate) // "YYYY-MM-DD"
    const scheduledAt = new Date(`${dateString}T${selectedSlot}Z`)

    try {
      await createAppointment.mutateAsync({
        barberId: selectedBarber.id,
        scheduledAt: scheduledAt.toISOString(),
        serviceIds: selectedServiceIds,
        notes: notes.trim() || undefined,
      })
      router.push('/client/dashboard')
    } catch {
      // Error handled by mutation state
    }
  }

  const selectedServices = allServices?.filter((s) => selectedServiceIds.includes(s.id)) ?? []

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
      {/* Page header */}
      <div className="mb-8">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">
          Novo Agendamento
        </h1>
        <p className="mt-1 text-sm text-brand-white/50">
          Siga os passos para agendar seu atendimento
        </p>
      </div>

      {/* Step indicator */}
      <nav aria-label="Progresso do agendamento" className="mb-8">
        <ol className="flex items-center gap-0">
          {STEP_LABELS.map((label, idx) => {
            const stepNum = (idx + 1) as Step
            const isCompleted = stepNum < step
            const isCurrent = stepNum === step

            return (
              <li key={label} className="flex items-center flex-1">
                <div className="flex flex-col items-center gap-1">
                  <div
                    className={[
                      'flex h-8 w-8 items-center justify-center rounded-full text-sm font-bold transition-colors',
                      isCompleted
                        ? 'bg-brand-gold text-brand-black'
                        : isCurrent
                        ? 'border-2 border-brand-gold text-brand-gold'
                        : 'border-2 border-brand-white/20 text-brand-white/30',
                    ].join(' ')}
                    aria-current={isCurrent ? 'step' : undefined}
                  >
                    {isCompleted ? (
                      <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                        <path
                          fillRule="evenodd"
                          d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                          clipRule="evenodd"
                        />
                      </svg>
                    ) : (
                      stepNum
                    )}
                  </div>
                  <span
                    className={[
                      'hidden sm:block text-xs font-medium',
                      isCurrent ? 'text-brand-gold' : isCompleted ? 'text-brand-gold/60' : 'text-brand-white/30',
                    ].join(' ')}
                  >
                    {label}
                  </span>
                </div>
                {idx < STEP_LABELS.length - 1 && (
                  <div
                    className={[
                      'flex-1 h-px mx-2 transition-colors',
                      isCompleted ? 'bg-brand-gold' : 'bg-brand-white/10',
                    ].join(' ')}
                    aria-hidden="true"
                  />
                )}
              </li>
            )
          })}
        </ol>
      </nav>

      {/* Step content */}
      <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6 mb-6">
        <h2 className="font-montserrat font-bold text-brand-white mb-6">
          {step === 1 && 'Escolha o Barbeiro'}
          {step === 2 && 'Escolha os Serviços'}
          {step === 3 && 'Escolha Data e Horário'}
          {step === 4 && 'Confirmar Agendamento'}
        </h2>

        {step === 1 && (
          <BarberPicker
            selectedBarberId={selectedBarber?.id ?? null}
            onSelect={(barber) => {
              setSelectedBarber(barber)
              // Reset downstream selections when barber changes
              setSelectedSlot(null)
            }}
          />
        )}

        {step === 2 && (
          <ServicePicker
            selectedServiceIds={selectedServiceIds}
            onToggle={toggleService}
          />
        )}

        {step === 3 && selectedBarber && (
          <SlotPicker
            barberId={selectedBarber.id}
            serviceIds={selectedServiceIds}
            barberAvailability={selectedBarber.availability}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            onDateChange={(d) => {
              setSelectedDate(d)
              setSelectedSlot(null) // Reset slot when date changes
            }}
            onSlotChange={setSelectedSlot}
          />
        )}

        {step === 4 && selectedBarber && selectedDate && selectedSlot && (
          <BookingConfirmation
            barber={selectedBarber}
            services={selectedServices}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            notes={notes}
            onNotesChange={setNotes}
            onConfirm={handleConfirm}
            isLoading={createAppointment.isPending}
          />
        )}

        {createAppointment.isError && (
          <p role="alert" className="mt-4 text-sm text-red-400">
            Erro ao criar agendamento. Tente novamente.
          </p>
        )}
      </div>

      {/* Navigation buttons (hide on step 4 since confirmation has its own button) */}
      {step < 4 && (
        <div className="flex justify-between">
          <Button
            variant="ghost"
            onClick={handleBack}
            disabled={step === 1}
          >
            Voltar
          </Button>
          <Button
            onClick={handleNext}
            disabled={!canAdvance()}
          >
            Próximo
          </Button>
        </div>
      )}
      {step === 4 && (
        <div className="flex justify-start">
          <Button variant="ghost" onClick={handleBack}>
            Voltar
          </Button>
        </div>
      )}
    </div>
  )
}
