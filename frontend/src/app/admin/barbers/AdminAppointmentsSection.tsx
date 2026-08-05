'use client'

import { useState } from 'react'
import {
  useAdminBarberAppointments,
  useAdminUpdateAppointmentPayment,
  useAdminCompleteAppointment,
  useAdminCancelAppointment,
} from '@/hooks/useAdminBarberAppointments'
import { Spinner } from '@/components/ui/Spinner'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import { getStatusConfig } from '@/lib/utils/statusConfig'
import type { Appointment, PaymentMethod } from '@/types/api.types'

const PAYMENT_METHODS: PaymentMethod[] = ['Dinheiro', 'Cartão', 'Pix']

type Filter = 'Accepted' | 'Completed' | 'Cancelled'

const FILTERS: { value: Filter; label: string }[] = [
  { value: 'Accepted', label: 'Confirmados' },
  { value: 'Completed', label: 'Concluídos' },
  { value: 'Cancelled', label: 'Cancelados' },
]

function total(appt: Appointment) {
  return appt.services.reduce((s, v) => s + v.price, 0)
}

/** Escolha da forma de pagamento, usada tanto ao concluir quanto ao registrar depois. */
function PaymentPicker({
  onPick,
  onCancel,
  allowSkip,
}: {
  onPick: (method?: PaymentMethod) => void
  onCancel: () => void
  allowSkip?: boolean
}) {
  return (
    <div className="flex flex-wrap items-center gap-1">
      {PAYMENT_METHODS.map((m) => (
        <button
          key={m}
          onClick={() => onPick(m)}
          className="rounded border border-brand-white/20 px-2 py-0.5 text-brand-white/60 transition-colors hover:border-brand-gold hover:text-brand-gold"
        >
          {m}
        </button>
      ))}
      {allowSkip && (
        <button
          onClick={() => onPick(undefined)}
          className="rounded border border-brand-white/10 px-2 py-0.5 text-brand-white/40 transition-colors hover:text-brand-white/70"
        >
          Sem registrar
        </button>
      )}
      <button onClick={onCancel} className="px-2 py-0.5 text-brand-white/30 hover:text-brand-white/60">
        ✕
      </button>
    </div>
  )
}

export default function AdminAppointmentsSection({ barberId }: { barberId: string }) {
  const { data: appointments, isLoading } = useAdminBarberAppointments(barberId)
  const updatePayment = useAdminUpdateAppointmentPayment(barberId)
  const completeAppointment = useAdminCompleteAppointment(barberId)
  const cancelAppointment = useAdminCancelAppointment(barberId)

  const [filter, setFilter] = useState<Filter>('Accepted')
  const [registeringId, setRegisteringId] = useState<string | null>(null)
  const [completingId, setCompletingId] = useState<string | null>(null)

  if (isLoading) return <div className="py-2"><Spinner size="sm" /></div>

  const all = appointments ?? []
  const shown = all
    .filter((a) => a.status === filter)
    .sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))

  return (
    <div className="mt-2">
      <div className="mb-3 flex flex-wrap gap-1">
        {FILTERS.map(({ value, label }) => {
          const count = all.filter((a) => a.status === value).length
          return (
            <button
              key={value}
              onClick={() => setFilter(value)}
              className={[
                'rounded-lg px-2.5 py-1 text-xs transition-colors',
                filter === value
                  ? 'bg-brand-gold/15 font-semibold text-brand-gold'
                  : 'text-brand-white/40 hover:text-brand-white/70',
              ].join(' ')}
            >
              {label} ({count})
            </button>
          )
        })}
      </div>

      {shown.length === 0 ? (
        <p className="py-2 text-xs text-brand-white/30">Nenhum atendimento nesta situação.</p>
      ) : (
        <div className="flex flex-col gap-1">
          {shown.slice(0, 20).map((appt) => {
            const status = getStatusConfig(appt.status)
            return (
              <div
                key={appt.id}
                className="flex flex-col gap-2 rounded-lg border border-brand-white/5 bg-brand-black px-3 py-2 text-xs text-brand-white/60"
              >
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                  <span className="font-medium text-brand-white/80">{appt.clientName}</span>
                  <span>{formatDateTime(appt.scheduledAt)}</span>
                  <span className="text-brand-gold">{formatCurrency(total(appt))}</span>
                  <span className={`rounded-full px-2 py-0.5 ${status.bgColor} ${status.color}`}>
                    {status.label}
                  </span>
                  {appt.paymentMethod && (
                    <span className="rounded-full bg-brand-gold/15 px-2 py-0.5 text-brand-gold">
                      {appt.paymentMethod}
                    </span>
                  )}
                </div>

                {appt.status === 'Accepted' && (
                  completingId === appt.id ? (
                    <PaymentPicker
                      allowSkip
                      onCancel={() => setCompletingId(null)}
                      onPick={async (paymentMethod) => {
                        try {
                          await completeAppointment.mutateAsync({ id: appt.id, paymentMethod })
                        } finally {
                          setCompletingId(null)
                        }
                      }}
                    />
                  ) : (
                    <div className="flex flex-wrap gap-2">
                      <button
                        onClick={() => setCompletingId(appt.id)}
                        className="rounded border border-brand-gold/40 px-2 py-0.5 text-brand-gold transition-colors hover:bg-brand-gold/10"
                      >
                        Concluir
                      </button>
                      <button
                        onClick={() => {
                          if (!window.confirm(`Cancelar o atendimento de ${appt.clientName}?`)) return
                          cancelAppointment.mutate({ id: appt.id })
                        }}
                        className="rounded border border-red-400/40 px-2 py-0.5 text-red-400 transition-colors hover:bg-red-400/10"
                      >
                        Cancelar
                      </button>
                    </div>
                  )
                )}

                {appt.status === 'Completed' && !appt.paymentMethod && (
                  registeringId === appt.id ? (
                    <PaymentPicker
                      onCancel={() => setRegisteringId(null)}
                      onPick={async (paymentMethod) => {
                        if (!paymentMethod) return
                        try {
                          await updatePayment.mutateAsync({ id: appt.id, paymentMethod })
                        } finally {
                          setRegisteringId(null)
                        }
                      }}
                    />
                  ) : (
                    <button
                      onClick={() => setRegisteringId(appt.id)}
                      className="self-start rounded border border-brand-white/20 px-2 py-0.5 text-brand-white/40 transition-colors hover:border-brand-gold/50 hover:text-brand-gold/70"
                    >
                      Registrar pagamento
                    </button>
                  )
                )}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
