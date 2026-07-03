'use client'

import { useState } from 'react'
import { useAdminBarberAppointments, useAdminUpdateAppointmentPayment } from '@/hooks/useAdminBarberAppointments'
import { Spinner } from '@/components/ui/Spinner'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import type { PaymentMethod } from '@/types/api.types'

const PAYMENT_METHODS: PaymentMethod[] = ['Dinheiro', 'Cartão', 'Pix']

export default function AdminAppointmentsSection({ barberId }: { barberId: string }) {
  const { data: appointments, isLoading } = useAdminBarberAppointments(barberId)
  const updatePayment = useAdminUpdateAppointmentPayment(barberId)
  const [registeringId, setRegisteringId] = useState<string | null>(null)

  const completed = appointments?.filter((a) => a.status === 'Completed') ?? []

  if (isLoading) return <div className="py-2"><Spinner size="sm" /></div>
  if (completed.length === 0) {
    return <p className="text-xs text-brand-white/30 py-2">Nenhum atendimento concluído.</p>
  }

  return (
    <div className="mt-2">
      <p className="text-xs font-semibold text-brand-white/40 mb-2 uppercase tracking-wide">
        Atendimentos concluídos
      </p>
      <div className="flex flex-col gap-1">
        {completed.slice(0, 10).map((appt) => (
          <div
            key={appt.id}
            className="flex flex-wrap items-center gap-3 rounded-lg border border-brand-white/5 bg-brand-black px-3 py-2 text-xs text-brand-white/60"
          >
            <span>{appt.clientName}</span>
            <span>{formatDateTime(appt.scheduledAt)}</span>
            <span className="text-brand-gold">{formatCurrency(appt.services.reduce((s, v) => s + v.price, 0))}</span>
            {appt.paymentMethod ? (
              <span className="rounded-full bg-brand-gold/15 px-2 py-0.5 text-brand-gold">
                {appt.paymentMethod}
              </span>
            ) : registeringId === appt.id ? (
              <div className="flex gap-1">
                {PAYMENT_METHODS.map((m) => (
                  <button
                    key={m}
                    onClick={async () => {
                      try {
                        await updatePayment.mutateAsync({ id: appt.id, paymentMethod: m })
                      } finally {
                        setRegisteringId(null)
                      }
                    }}
                    className="px-2 py-0.5 rounded border border-brand-white/20 text-brand-white/60 hover:border-brand-gold hover:text-brand-gold transition-colors"
                  >
                    {m}
                  </button>
                ))}
                <button
                  onClick={() => setRegisteringId(null)}
                  className="px-2 py-0.5 text-brand-white/30 hover:text-brand-white/60"
                >
                  ✕
                </button>
              </div>
            ) : (
              <button
                onClick={() => setRegisteringId(appt.id)}
                className="rounded border border-brand-white/20 px-2 py-0.5 text-brand-white/40 hover:border-brand-gold/50 hover:text-brand-gold/70 transition-colors"
              >
                Registrar pagamento
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
