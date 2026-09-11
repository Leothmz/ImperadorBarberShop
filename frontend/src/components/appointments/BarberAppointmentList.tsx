'use client'

import { useState } from 'react'
import { AppointmentCard } from './AppointmentCard'
import { PlanPaymentModal, type PlanPaymentAction } from './PlanPaymentModal'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useCancelAppointmentByBarber,
  useCompleteAppointment,
  useUpdatePaymentMethod,
} from '@/hooks/useAppointments'
import type { Appointment, AppointmentPayment, PaymentMethod, PlanTender } from '@/types/api.types'

// Plano não é uma escolha direta: abre o modal que pergunta Pagamento ou Recorrência
const PAYMENT_OPTIONS: { value: PaymentMethod | null; label: string }[] = [
  { value: 'Dinheiro', label: '💵 Dinheiro' },
  { value: 'Cartão', label: '💳 Cartão' },
  { value: 'Pix', label: '⚡ Pix' },
  { value: 'Plano', label: '📋 Plano' },
  { value: null, label: 'Pular' },
]

const REGISTER_METHODS: PaymentMethod[] = ['Dinheiro', 'Cartão', 'Pix', 'Plano']

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const cancel = useCancelAppointmentByBarber()
  const complete = useCompleteAppointment()
  const updatePayment = useUpdatePaymentMethod()
  const [pendingCompleteId, setPendingCompleteId] = useState<string | null>(null)
  const [pendingPaymentId, setPendingPaymentId] = useState<string | null>(null)
  const [selectedMethod, setSelectedMethod] = useState<PlanTender | null>(null)
  const [planFor, setPlanFor] = useState<{ appointment: Appointment; action: PlanPaymentAction } | null>(null)

  if (isLoading) {
    return <div className="flex justify-center py-12"><Spinner size="lg" /></div>
  }
  if (isError) {
    return <p role="alert" className="text-center text-brand-gold/70 py-8">Erro ao carregar agendamentos.</p>
  }
  if (!appointments || appointments.length === 0) {
    return <p className="text-center text-brand-white/50 py-8">Nenhum agendamento encontrado.</p>
  }

  const sorted = [...appointments].sort(
    (a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()
  )

  async function handleComplete(id: string, payment?: AppointmentPayment) {
    await complete.mutateAsync({ id, payment })
    setPendingCompleteId(null)
    setSelectedMethod(null)
  }

  async function confirmPlan(payment: AppointmentPayment) {
    if (!planFor) return
    const { appointment, action } = planFor
    if (action === 'complete') {
      await handleComplete(appointment.id, payment)
    } else {
      await updatePayment.mutateAsync({ id: appointment.id, payment })
      setPendingPaymentId(null)
    }
    setPlanFor(null)
  }

  return (
    <div className="flex flex-col gap-3">
      {sorted.map((appointment) => (
        <AppointmentCard
          key={appointment.id}
          appointment={appointment}
          actions={
            appointment.status === 'Accepted' ? (
              pendingCompleteId === appointment.id ? (
                <div className="flex flex-col gap-3 w-full">
                  <p className="text-sm text-brand-white/70">Forma de pagamento (opcional)</p>
                  <div className="flex flex-wrap gap-2">
                    {PAYMENT_OPTIONS.map((opt) => (
                      <button
                        key={opt.label}
                        onClick={() =>
                          opt.value === 'Plano'
                            ? setPlanFor({ appointment, action: 'complete' })
                            : setSelectedMethod(opt.value)
                        }
                        className={[
                          'px-3 py-1.5 rounded-lg text-sm border transition-colors',
                          selectedMethod === opt.value
                            ? 'border-brand-gold bg-brand-gold/20 text-brand-gold'
                            : 'border-brand-white/20 text-brand-white/60 hover:border-brand-gold/50',
                        ].join(' ')}
                      >
                        {opt.label}
                      </button>
                    ))}
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      isLoading={complete.isPending}
                      onClick={() =>
                        handleComplete(appointment.id, selectedMethod ? { paymentMethod: selectedMethod } : undefined)
                      }
                    >
                      Confirmar
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => { setPendingCompleteId(null); setSelectedMethod(null) }}
                    >
                      Cancelar
                    </Button>
                  </div>
                </div>
              ) : (
                <>
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => { setPendingCompleteId(appointment.id); setSelectedMethod(null) }}
                  >
                    Concluir
                  </Button>
                  <Button
                    variant="danger"
                    size="sm"
                    isLoading={cancel.isPending && cancel.variables === appointment.id}
                    onClick={() => cancel.mutate(appointment.id)}
                  >
                    Cancelar
                  </Button>
                </>
              )
            ) : appointment.status === 'Completed' && !appointment.paymentMethod ? (
              pendingPaymentId === appointment.id ? (
                <div className="flex flex-wrap gap-2 items-center">
                  {REGISTER_METHODS.map((m) => (
                    <button
                      key={m}
                      onClick={async () => {
                        if (m === 'Plano') {
                          setPlanFor({ appointment, action: 'register' })
                          return
                        }
                        try {
                          await updatePayment.mutateAsync({ id: appointment.id, payment: { paymentMethod: m } })
                        } finally {
                          setPendingPaymentId(null)
                        }
                      }}
                      className="px-3 py-1 rounded-lg text-xs border border-brand-white/20 text-brand-white/60 hover:border-brand-gold hover:text-brand-gold transition-colors"
                    >
                      {m}
                    </button>
                  ))}
                  <button
                    onClick={() => setPendingPaymentId(null)}
                    className="text-xs text-brand-white/40 hover:text-brand-white/70"
                  >
                    Fechar
                  </button>
                </div>
              ) : (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => setPendingPaymentId(appointment.id)}
                >
                  Registrar pagamento
                </Button>
              )
            ) : undefined
          }
        />
      ))}

      {planFor && (
        <PlanPaymentModal
          clientName={planFor.appointment.clientName}
          action={planFor.action}
          onClose={() => setPlanFor(null)}
          onConfirm={confirmPlan}
        />
      )}
    </div>
  )
}
