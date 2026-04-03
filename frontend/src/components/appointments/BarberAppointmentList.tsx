'use client'

import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useAcceptAppointment,
  useRejectAppointment,
  useCompleteAppointment,
} from '@/hooks/useAppointments'

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const accept = useAcceptAppointment()
  const reject = useRejectAppointment()
  const complete = useCompleteAppointment()

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError) {
    return (
      <p role="alert" className="text-center text-red-400 py-8">
        Erro ao carregar agendamentos.
      </p>
    )
  }

  if (!appointments || appointments.length === 0) {
    return (
      <p className="text-center text-brand-white/50 py-8">
        Nenhum agendamento encontrado.
      </p>
    )
  }

  // Sort: Pending first, then by date
  const sorted = [...appointments].sort((a, b) => {
    if (a.status === 'Pending' && b.status !== 'Pending') return -1
    if (a.status !== 'Pending' && b.status === 'Pending') return 1
    return new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()
  })

  return (
    <div className="flex flex-col gap-3">
      {sorted.map((appointment) => (
        <AppointmentCard
          key={appointment.id}
          appointment={appointment}
          actions={
            <>
              {appointment.status === 'Pending' && (
                <>
                  <Button
                    variant="primary"
                    size="sm"
                    isLoading={accept.isPending && accept.variables === appointment.id}
                    onClick={() => accept.mutate(appointment.id)}
                  >
                    Aceitar
                  </Button>
                  <Button
                    variant="danger"
                    size="sm"
                    isLoading={reject.isPending && reject.variables === appointment.id}
                    onClick={() => reject.mutate(appointment.id)}
                  >
                    Recusar
                  </Button>
                </>
              )}
              {appointment.status === 'Accepted' && (
                <Button
                  variant="secondary"
                  size="sm"
                  isLoading={complete.isPending && complete.variables === appointment.id}
                  onClick={() => complete.mutate(appointment.id)}
                >
                  Concluir
                </Button>
              )}
            </>
          }
        />
      ))}
    </div>
  )
}
