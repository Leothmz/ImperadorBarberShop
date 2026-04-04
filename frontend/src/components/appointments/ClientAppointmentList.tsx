'use client'

import { useState } from 'react'
import { AppointmentCard } from './AppointmentCard'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { ReviewForm } from './ReviewForm'
import { Spinner } from '@/components/ui/Spinner'
import { useClientAppointments, useCancelAppointment } from '@/hooks/useAppointments'
import type { Appointment } from '@/types/api.types'

type Tab = 'upcoming' | 'history'

export function ClientAppointmentList() {
  const { data: appointments, isLoading, isError } = useClientAppointments()
  const cancelAppointment = useCancelAppointment()
  const [activeTab, setActiveTab] = useState<Tab>('upcoming')
  const [reviewAppointment, setReviewAppointment] = useState<Appointment | null>(null)
  const [reviewedIds, setReviewedIds] = useState<Set<string>>(new Set())
  const [cancelError, setCancelError] = useState<string | null>(null)

  // Returns true when the appointment is still eligible for cancellation:
  // status is Pending or Accepted, and it starts more than 2 hours from now.
  function isCancellable(appointment: Appointment): boolean {
    if (appointment.status !== 'Pending' && appointment.status !== 'Accepted') {
      return false
    }
    const twoHoursFromNow = Date.now() + 2 * 60 * 60 * 1000
    return new Date(appointment.scheduledAt).getTime() > twoHoursFromNow
  }

  async function handleCancel(appointment: Appointment) {
    setCancelError(null)
    try {
      await cancelAppointment.mutateAsync(appointment.id)
    } catch {
      setCancelError('Não foi possível cancelar o agendamento. Tente novamente.')
    }
  }

  const now = new Date()

  const upcoming =
    appointments?.filter(
      (a) =>
        (a.status === 'Pending' || a.status === 'Accepted') &&
        new Date(a.scheduledAt) >= now
    ) ?? []

  const history =
    appointments?.filter(
      (a) =>
        a.status === 'Completed' ||
        a.status === 'Cancelled' ||
        a.status === 'Rejected' ||
        new Date(a.scheduledAt) < now
    ) ?? []

  const displayed = activeTab === 'upcoming' ? upcoming : history

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

  return (
    <div className="flex flex-col gap-4">
      {/* Tabs */}
      <div className="flex gap-1 rounded-lg bg-brand-black-soft p-1 w-fit">
        <button
          onClick={() => setActiveTab('upcoming')}
          className={[
            'rounded-md px-4 py-2 text-sm font-medium transition-colors cursor-pointer',
            activeTab === 'upcoming'
              ? 'bg-brand-gold text-brand-black'
              : 'text-brand-white/60 hover:text-brand-white',
          ].join(' ')}
          aria-pressed={activeTab === 'upcoming'}
        >
          Próximos ({upcoming.length})
        </button>
        <button
          onClick={() => setActiveTab('history')}
          className={[
            'rounded-md px-4 py-2 text-sm font-medium transition-colors cursor-pointer',
            activeTab === 'history'
              ? 'bg-brand-gold text-brand-black'
              : 'text-brand-white/60 hover:text-brand-white',
          ].join(' ')}
          aria-pressed={activeTab === 'history'}
        >
          Histórico ({history.length})
        </button>
      </div>

      {/* List */}
      {displayed.length === 0 ? (
        <p className="text-center text-brand-white/50 py-8">
          {activeTab === 'upcoming'
            ? 'Nenhum agendamento próximo.'
            : 'Nenhum agendamento no histórico.'}
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {displayed.map((appointment) => {
            const cancellable = isCancellable(appointment)
            const isCancelling =
              cancelAppointment.isPending &&
              cancelAppointment.variables === appointment.id

            const showReview =
              appointment.status === 'Completed' &&
              !appointment.hasReview &&
              !reviewedIds.has(appointment.id)

            return (
              <AppointmentCard
                key={appointment.id}
                appointment={appointment}
                actions={
                  showReview || cancellable ? (
                    <>
                      {/* Show "Avaliar" only when the appointment is Completed AND
                          neither the API (hasReview) nor the local optimistic state
                          indicates a review has already been submitted. This prevents
                          a 422 duplicate-review error from the backend. */}
                      {showReview && (
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => setReviewAppointment(appointment)}
                        >
                          Avaliar
                        </Button>
                      )}
                      {cancellable && (
                        <Button
                          variant="ghost"
                          size="sm"
                          isLoading={isCancelling}
                          onClick={() => handleCancel(appointment)}
                          className="text-red-400 hover:text-red-300 hover:bg-red-400/10"
                        >
                          Cancelar
                        </Button>
                      )}
                    </>
                  ) : undefined
                }
              />
            )
          })}
        </div>
      )}

      {cancelError && (
        <p role="alert" className="text-sm text-red-400 text-center">
          {cancelError}
        </p>
      )}

      {/* Review modal */}
      <Modal
        isOpen={!!reviewAppointment}
        onClose={() => setReviewAppointment(null)}
        title="Avaliar serviço"
      >
        {reviewAppointment && (
          <ReviewForm
            appointmentId={reviewAppointment.id}
            onSuccess={() => {
              setReviewedIds((prev) => new Set(prev).add(reviewAppointment.id))
              setReviewAppointment(null)
            }}
          />
        )}
      </Modal>
    </div>
  )
}
