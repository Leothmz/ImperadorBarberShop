import { Badge } from '@/components/ui/Badge'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'
import { PAYMENT_ICONS, describePayment, describePlanAmount } from '@/lib/utils/payment'
import type { Appointment } from '@/types/api.types'

interface AppointmentCardProps {
  appointment: Appointment
  actions?: React.ReactNode
}

export function AppointmentCard({ appointment, actions }: AppointmentCardProps) {
  const payment = describePayment(appointment)
  const planAmount = describePlanAmount(appointment)

  return (
    <article
      className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-5 flex flex-col gap-3"
      aria-label={`Agendamento com ${appointment.barberName}`}
    >
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="font-montserrat font-semibold text-brand-white">
            {appointment.barberName}
          </p>
          <p className="text-sm text-brand-white/50">
            Cliente: {appointment.clientName} · {appointment.clientPhone}
          </p>
        </div>
        <Badge status={appointment.status} />
      </div>

      {/* Services */}
      <div className="flex flex-wrap gap-1">
        {appointment.services.map((s) => (
          <span
            key={s.id}
            className="rounded-full bg-brand-white/10 px-2.5 py-0.5 text-xs text-brand-white/70"
          >
            {s.name}
          </span>
        ))}
      </div>

      {/* Details */}
      <div className="flex flex-wrap items-center gap-4 text-sm text-brand-white/60">
        <span>{formatDateTime(appointment.scheduledAt)}</span>
        <span>{formatDuration(appointment.totalDurationMinutes)}</span>
        <span className="font-semibold text-brand-gold">{formatCurrency(appointment.effectiveAmount)}</span>
      </div>

      {appointment.status === 'Completed' && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          {appointment.paymentMethod && payment ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-brand-gold/15 px-2.5 py-0.5 text-xs font-medium text-brand-gold">
              {PAYMENT_ICONS[appointment.paymentMethod]} {payment}
            </span>
          ) : (
            <span className="text-xs text-brand-white/30">— sem método</span>
          )}
          {planAmount && <span className="text-xs text-brand-white/60">{planAmount}</span>}
        </div>
      )}

      {appointment.notes && (
        <p className="text-xs text-brand-white/40 italic">&quot;{appointment.notes}&quot;</p>
      )}

      {/* Actions */}
      {actions && <div className="flex flex-wrap gap-2 pt-1">{actions}</div>}
    </article>
  )
}
