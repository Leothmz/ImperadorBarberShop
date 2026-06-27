'use client'

import { useParams } from 'next/navigation'
import { useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { Badge } from '@/components/ui/Badge'
import { ReviewForm } from '@/components/appointments/ReviewForm'
import { useAppointmentByToken, useCancelAppointmentByToken } from '@/hooks/useAppointments'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'

export function ManageAppointmentView({ token }: { token: string }) {
  const { data: appointment, isLoading, isError } = useAppointmentByToken(token)
  const cancelAppointment = useCancelAppointmentByToken(token)
  const [reviewSubmitted, setReviewSubmitted] = useState(false)

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError || !appointment) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-10 text-center">
        <p role="alert" className="text-red-400">
          Agendamento não encontrado. Verifique o link recebido.
        </p>
      </div>
    )
  }

  const totalPrice = appointment.services.reduce((acc, s) => acc + s.price, 0)
  const canCancel =
    appointment.status === 'Accepted' &&
    // eslint-disable-next-line react-hooks/purity -- time-gated UI affordance, not render-critical state
    new Date(appointment.scheduledAt).getTime() - Date.now() > 2 * 60 * 60 * 1000

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6 flex flex-col gap-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h1 className="font-montserrat text-xl font-bold text-brand-white">
              {appointment.barberName}
            </h1>
            <p className="text-sm text-brand-white/50">{appointment.clientName}</p>
          </div>
          <Badge status={appointment.status} />
        </div>

        <div className="flex flex-wrap gap-1">
          {appointment.services.map((s) => (
            <span key={s.id} className="rounded-full bg-brand-white/10 px-2.5 py-0.5 text-xs text-brand-white/70">
              {s.name}
            </span>
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-4 text-sm text-brand-white/60">
          <span>{formatDateTime(appointment.scheduledAt)}</span>
          <span>{formatDuration(appointment.totalDurationMinutes)}</span>
          <span className="font-semibold text-brand-gold">{formatCurrency(totalPrice)}</span>
        </div>

        {appointment.status === 'Accepted' && (
          <Button
            variant="danger"
            isLoading={cancelAppointment.isPending}
            disabled={!canCancel}
            onClick={() => cancelAppointment.mutate()}
          >
            {canCancel ? 'Cancelar agendamento' : 'Cancelamento indisponível (menos de 2h)'}
          </Button>
        )}

        {appointment.status === 'Cancelled' && (
          <p className="text-sm text-brand-white/60">Este agendamento foi cancelado.</p>
        )}

        {appointment.status === 'Completed' && !reviewSubmitted && (
          <ReviewForm accessToken={token} onSuccess={() => setReviewSubmitted(true)} />
        )}

        {appointment.status === 'Completed' && reviewSubmitted && (
          <p className="text-sm text-brand-gold">Obrigado pela sua avaliação!</p>
        )}
      </div>
    </div>
  )
}

export default function ManageAppointmentPage() {
  const params = useParams<{ token: string }>()
  return <ManageAppointmentView token={params.token} />
}
