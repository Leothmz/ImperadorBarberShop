'use client'

import { useCallback, useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { useQueryClient } from '@tanstack/react-query'
import { BarberPicker } from '@/components/booking/BarberPicker'
import { ServicePicker } from '@/components/booking/ServicePicker'
import { SlotPicker } from '@/components/booking/SlotPicker'
import { BookingConfirmation } from '@/components/booking/BookingConfirmation'
import { Button } from '@/components/ui/Button'
import { useCreateAppointment } from '@/hooks/useAppointments'
import { useServices } from '@/hooks/useServices'
import { useBarbers } from '@/hooks/useBarbers'
import { normalizeBrPhone } from '@/lib/utils/phone'
import { toApiDate } from '@/lib/utils/formatDateTime'
import { describeBookingError } from '@/lib/utils/appointmentError'
import { shopWhatsappLink } from '@/lib/utils/whatsapp'
import {
  clearDraft,
  loadDraft,
  parseDraftDate,
  saveDraft,
  serializeDraftDate,
} from '@/lib/utils/bookingDraft'
import type { Barber, Service } from '@/types/api.types'

type Step = 1 | 2 | 3 | 4

const STEP_LABELS = ['Barbeiro', 'Serviços', 'Data e Horário', 'Confirmar']

function toStep(value: number): Step {
  return Math.min(Math.max(Math.trunc(value) || 1, 1), 4) as Step
}

export default function AgendarPage() {
  const router = useRouter()
  const queryClient = useQueryClient()
  const [step, setStep] = useState<Step>(1)

  const [selectedBarber, setSelectedBarber] = useState<Barber | null>(null)
  const [selectedServiceIds, setSelectedServiceIds] = useState<string[]>([])
  const [selectedDate, setSelectedDate] = useState<Date | null>(null)
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null)
  const [notes, setNotes] = useState('')
  const [clientName, setClientName] = useState('')
  const [clientPhone, setClientPhone] = useState('')
  // Só persiste depois de restaurar: gravar antes apagaria o rascunho com os
  // estados vazios do primeiro render.
  const [restored, setRestored] = useState(false)

  const { data: allServices } = useServices()
  // Mesma query que o BarberPicker usa: vem do cache, sem requisição extra.
  const { data: allBarbers } = useBarbers()
  const createAppointment = useCreateAppointment()
  // `?barbeiro=<id>` vem da fila de barbeiros da home: o cliente já escolheu.
  const [pendingBarberId, setPendingBarberId] = useState<string | null>(null)

  // O público chega de link do WhatsApp e troca de app no meio do fluxo — sem
  // rascunho, voltar para a aba recomeça do passo 1 com tudo perdido.
  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hidratação: o rascunho vive
       no sessionStorage, que não existe no servidor. Ler durante o render faria o
       cliente divergir do HTML já enviado. */
    const requestedBarber = new URLSearchParams(window.location.search).get('barbeiro')
    if (requestedBarber) setPendingBarberId(requestedBarber)

    const draft = loadDraft()
    if (draft) {
      setSelectedBarber(draft.barber)
      setSelectedServiceIds(draft.serviceIds)
      setSelectedDate(parseDraftDate(draft.date))
      setSelectedSlot(draft.slot)
      setClientName(draft.clientName)
      setClientPhone(draft.clientPhone)
      setNotes(draft.notes)
      setStep(toStep(draft.step))
      window.history.replaceState({ passo: draft.step }, '', `?passo=${draft.step}`)
    }
    setRestored(true)
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [])

  useEffect(() => {
    if (!restored) return
    saveDraft({
      step,
      barber: selectedBarber,
      serviceIds: selectedServiceIds,
      date: serializeDraftDate(selectedDate),
      slot: selectedSlot,
      clientName,
      clientPhone,
      notes,
    })
  }, [
    restored,
    step,
    selectedBarber,
    selectedServiceIds,
    selectedDate,
    selectedSlot,
    clientName,
    clientPhone,
    notes,
  ])

  // O passo vive no histórico para que o Voltar do navegador volte um passo em
  // vez de abandonar o agendamento inteiro.
  useEffect(() => {
    function handlePopState(event: PopStateEvent) {
      const passo = (event.state as { passo?: number } | null)?.passo
      setStep(toStep(Number(passo ?? 1)))
    }
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const goToStep = useCallback((next: Step) => {
    setStep(next)
    window.history.pushState({ passo: next }, '', `?passo=${next}`)
  }, [])

  // Escolha vinda da home: assim que os barbeiros carregam, pula o passo 1.
  useEffect(() => {
    if (!pendingBarberId || !allBarbers) return
    const barber = allBarbers.find((b) => b.id === pendingBarberId)
    /* eslint-disable react-hooks/set-state-in-effect -- a escolha chega pela URL e
       o barbeiro só existe depois que a lista carrega; não há como resolver isso
       durante o render. */
    setPendingBarberId(null)
    if (!barber) return
    setSelectedBarber(barber)
    setSelectedSlot(null)
    goToStep(2)
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [pendingBarberId, allBarbers, goToStep])

  function toggleService(service: Service) {
    // Trocar serviço muda a duração total, e a duração total decide quais
    // horários cabem: manter o slot antigo enviaria um horário que não serve mais.
    setSelectedSlot(null)
    setSelectedServiceIds(prev => {
      if (prev.includes(service.id)) {
        const addonIds = new Set((service.addons ?? []).map(a => a.id))
        return prev.filter(id => id !== service.id && !addonIds.has(id))
      }
      return [...prev, service.id]
    })
  }

  function toggleAddon(addonId: string) {
    setSelectedSlot(null)
    setSelectedServiceIds((prev) =>
      prev.includes(addonId) ? prev.filter((id) => id !== addonId) : [...prev, addonId]
    )
  }

  function canAdvance(): boolean {
    if (step === 1) return !!selectedBarber
    if (step === 2) return selectedServiceIds.length > 0
    if (step === 3) return !!selectedDate && !!selectedSlot
    return true
  }

  function handleNext() {
    if (step < 4) goToStep((step + 1) as Step)
  }

  function handleBack() {
    if (step > 1) goToStep((step - 1) as Step)
  }

  function backToSlots() {
    createAppointment.reset()
    setSelectedSlot(null)
    queryClient.invalidateQueries({ queryKey: ['slots'] })
    goToStep(3)
  }

  async function handleConfirm() {
    if (!selectedBarber || !selectedDate || !selectedSlot) return

    const dateString = toApiDate(selectedDate)
    // Horário de parede, sem fuso: é o mesmo formato dos slots e do que a API
    // devolve. Converter para UTC aqui deslocaria o agendamento em 3 horas.
    const scheduledAt = `${dateString}T${selectedSlot}`

    try {
      const result = await createAppointment.mutateAsync({
        clientName: clientName.trim(),
        clientPhone: normalizeBrPhone(clientPhone),
        barberId: selectedBarber.id,
        scheduledAt,
        serviceIds: selectedServiceIds,
        notes: notes.trim() || undefined,
      })
      clearDraft()
      // `novo=1` faz a página de gestão abrir como confirmação, e não como o
      // mesmo card que um link velho abriria semanas depois.
      router.push(`/agendamento/${result.accessToken}?novo=1`)
    } catch {
      // Tratado por `bookingError` abaixo, com a recuperação certa para cada caso.
    }
  }

  const selectedServices = allServices?.filter((s) => selectedServiceIds.includes(s.id)) ?? []

  const selectedAddons = (allServices ?? [])
    .flatMap(s => s.addons ?? [])
    .filter(a => selectedServiceIds.includes(a.id))
    .map(a => ({ ...a, isActive: true, addons: [] } as Service))

  const selectedServicesForDisplay = [...selectedServices, ...selectedAddons]

  const bookingError = createAppointment.isError
    ? describeBookingError(createAppointment.error)
    : null
  const contactLink = shopWhatsappLink('Olá! Tentei agendar pelo site e não consegui.')

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
      <div className="mb-8">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">Agende seu horário</h1>
        <p className="mt-1 text-sm text-brand-white/50">Sem cadastro — leva menos de um minuto</p>
      </div>

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
                  {/* O rótulo do passo atual fica visível no celular: sem ele o
                      indicador vira quatro dígitos nus no aparelho que mais importa. */}
                  <span
                    className={[
                      'text-[0.65rem] leading-tight text-center sm:text-xs font-medium',
                      isCurrent ? 'text-brand-gold' : 'hidden sm:block',
                      isCompleted ? 'text-brand-gold/60' : '',
                      !isCurrent && !isCompleted ? 'text-brand-white/30' : '',
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
              setSelectedSlot(null)
            }}
          />
        )}

        {step === 2 && (
          <ServicePicker
            selectedServiceIds={selectedServiceIds}
            onToggle={toggleService}
            onToggleAddon={toggleAddon}
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
              setSelectedSlot(null)
            }}
            onSlotChange={setSelectedSlot}
          />
        )}

        {step === 4 && selectedBarber && selectedDate && selectedSlot && (
          <BookingConfirmation
            barber={selectedBarber}
            services={selectedServicesForDisplay}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            notes={notes}
            onNotesChange={setNotes}
            clientName={clientName}
            onClientNameChange={setClientName}
            clientPhone={clientPhone}
            onClientPhoneChange={setClientPhone}
            onConfirm={handleConfirm}
            isLoading={createAppointment.isPending}
          />
        )}

        {bookingError && (
          <div
            role="alert"
            className="mt-4 flex flex-col gap-3 rounded-lg border border-red-500/40 bg-red-500/10 p-4"
          >
            <p className="text-sm text-red-300">{bookingError.message}</p>

            {bookingError.action === 'pick-another-slot' && (
              <Button variant="secondary" size="sm" onClick={backToSlots} className="self-start">
                Escolher outro horário
              </Button>
            )}

            {bookingError.action === 'contact' && contactLink && (
              <a
                href={contactLink}
                target="_blank"
                rel="noopener noreferrer"
                className="self-start rounded-md border border-brand-gold px-3 py-1.5 text-sm text-brand-gold transition-colors hover:bg-brand-gold/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black"
              >
                Falar com a barbearia
              </a>
            )}

            {bookingError.action === 'retry' && (
              <Button
                variant="secondary"
                size="sm"
                onClick={handleConfirm}
                isLoading={createAppointment.isPending}
                className="self-start"
              >
                Tentar de novo
              </Button>
            )}
          </div>
        )}
      </div>

      {step < 4 && (
        <div className="flex justify-between">
          <Button variant="ghost" onClick={handleBack} disabled={step === 1}>
            Voltar
          </Button>
          <Button onClick={handleNext} disabled={!canAdvance()}>
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
