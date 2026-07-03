'use client'

import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import {
  useBarberAppointments,
  useCancelAppointmentByBarber,
  useCompleteAppointment,
} from '@/hooks/useAppointments'

export function BarberAppointmentList() {
  const { data: appointments, isLoading, isError } = useBarberAppointments()
  const cancel = useCancelAppointmentByBarber()
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

  const sorted = [...appointments].sort(
    (a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()
  )

  return (
    <div className="flex flex-col gap-3">
      {sorted.map((appointment) => (
        <AppointmentCard
          key={appointment.id}
          appointment={appointment}
          actions={
            appointment.status === 'Accepted' ? (
              <>
                <Button
                  variant="secondary"
                  size="sm"
                  isLoading={complete.isPending && complete.variables?.id === appointment.id}
                  onClick={() => complete.mutate({ id: appointment.id })}
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
            ) : undefined
          }
        />
      ))}
    </div>
  )
}
