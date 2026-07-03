'use client'

import { useState } from 'react'
import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useCancelAppointmentByBarber,
  useCompleteAppointment,
  useUpdatePaymentMethod,
} from '@/hooks/useAppointments'
import type { PaymentMethod } from '@/types/api.types'

const PAYMENT_OPTIONS: { value: PaymentMethod | null; label: string }[] = [
  { value: 'Dinheiro', label: '💵 Dinheiro' },
  { value: 'Cartão', label: '💳 Cartão' },
  { value: 'Pix', label: '⚡ Pix' },
  { value: null, label: 'Pular' },
]

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const cancel = useCancelAppointmentByBarber()
  const complete = useCompleteAppointment()
  const updatePayment = useUpdatePaymentMethod()
  const [pendingCompleteId, setPendingCompleteId] = useState<string | null>(null)
  const [pendingPaymentId, setPendingPaymentId] = useState<string | null>(null)
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod | null>(null)

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

  async function handleComplete(id: string, paymentMethod: PaymentMethod | null) {
    await complete.mutateAsync({ id, paymentMethod: paymentMethod ?? undefined })
    setPendingCompleteId(null)
    setSelectedMethod(null)
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
                        onClick={() => setSelectedMethod(opt.value)}
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
                      onClick={() => handleComplete(appointment.id, selectedMethod)}
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
                  {(['Dinheiro', 'Cartão', 'Pix'] as PaymentMethod[]).map((m) => (
                    <button
                      key={m}
                      onClick={async () => {
                        try {
                          await updatePayment.mutateAsync({ id: appointment.id, paymentMethod: m })
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
    </div>
  )
}
