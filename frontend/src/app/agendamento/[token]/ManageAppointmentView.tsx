'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { Button, buttonClasses } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { ReviewForm } from '@/components/appointments/ReviewForm'
import { useAppointmentByToken, useCancelAppointmentByToken } from '@/hooks/useAppointments'
import { formatDateTime, formatCurrency } from '@/lib/utils/formatDateTime'
import { formatDuration } from '@/lib/utils/formatDuration'
import { buildAppointmentIcs, downloadIcs } from '@/lib/utils/ics'
import { shopWhatsappLink } from '@/lib/utils/whatsapp'

interface ManageAppointmentViewProps {
  token: string
  /** Vem de `?novo=1` logo após agendar: abre a página como confirmação. */
  isNew?: boolean
}

export function ManageAppointmentView({ token, isNew = false }: ManageAppointmentViewProps) {
  const { data: appointment, isLoading, isError } = useAppointmentByToken(token)
  const cancelAppointment = useCancelAppointmentByToken(token)
  const [reviewSubmitted, setReviewSubmitted] = useState(false)
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const [copied, setCopied] = useState(false)
  const [manageUrl, setManageUrl] = useState('')

  // Montado no cliente: o link é o que o cliente precisa guardar, e no servidor
  // não existe origin para montá-lo.
  useEffect(() => {
    setManageUrl(`${window.location.origin}/agendamento/${token}`)
  }, [token])

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError || !appointment) {
    return (
      <div className="mx-auto flex max-w-2xl flex-col items-center gap-4 px-4 py-10 text-center">
        <p role="alert" className="text-red-400">
          Agendamento não encontrado. Verifique o link recebido.
        </p>
        <Link href="/agendar" className={buttonClasses()}>
          Fazer um novo agendamento
        </Link>
      </div>
    )
  }

  const totalPrice = appointment.services.reduce((acc, s) => acc + s.price, 0)
  const canCancel =
    appointment.status === 'Accepted' &&
    // eslint-disable-next-line react-hooks/purity -- time-gated UI affordance, not render-critical state
    new Date(appointment.scheduledAt).getTime() - Date.now() > 2 * 60 * 60 * 1000

  const contactLink = shopWhatsappLink(
    `Olá! Preciso falar sobre meu agendamento de ${formatDateTime(appointment.scheduledAt)}.`
  )

  async function handleShare() {
    if (!manageUrl) return
    const text = 'Meu agendamento na O Imperador Barber Shop'

    if (typeof navigator !== 'undefined' && navigator.share) {
      try {
        await navigator.share({ title: text, url: manageUrl })
        return
      } catch {
        // Compartilhamento cancelado: cai para a cópia.
      }
    }

    try {
      await navigator.clipboard.writeText(manageUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2500)
    } catch {
      // Sem clipboard (http, permissão negada): o link segue visível na barra.
    }
  }

  function handleAddToCalendar() {
    if (!appointment) return
    const ics = buildAppointmentIcs({
      scheduledAt: appointment.scheduledAt,
      durationMinutes: appointment.totalDurationMinutes,
      barberName: appointment.barberName,
      serviceNames: appointment.services.map((s) => s.name),
      manageUrl,
    })
    downloadIcs('agendamento-imperador.ics', ics)
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4 px-4 py-10 sm:px-6">
      {isNew && appointment.status === 'Accepted' && (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-brand-gold/30 bg-brand-gold/10 p-6 text-center">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-gold text-brand-black">
            <svg className="h-6 w-6" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
              <path
                fillRule="evenodd"
                d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
                clipRule="evenodd"
              />
            </svg>
          </span>
          <h1 className="font-montserrat text-xl font-black text-brand-white">
            Agendamento confirmado
          </h1>
          <p className="text-sm text-brand-white/70">
            Guarde este link — é por aqui que você cancela ou avalia depois.
          </p>
        </div>
      )}

      <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6 flex flex-col gap-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <h2 className="font-montserrat text-xl font-bold text-brand-white">
              {appointment.barberName}
            </h2>
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
          <div className="flex flex-col gap-3 border-t border-brand-white/10 pt-4">
            <div className="flex flex-wrap gap-2">
              {/* Logo após agendar, guardar o link é A ação: o cliente não tem
                  outro acesso ao agendamento. */}
              <Button variant={isNew ? 'primary' : 'secondary'} onClick={handleShare}>
                {copied ? 'Link copiado' : 'Guardar link'}
              </Button>
              <Button variant="secondary" onClick={handleAddToCalendar}>
                Adicionar à agenda
              </Button>
            </div>

            {canCancel ? (
              /* Destrutivo e discreto: a confirmação de verdade está no modal, então
                 o gatilho não precisa gritar em vermelho no meio do sucesso. */
              <Button
                variant="ghost"
                isLoading={cancelAppointment.isPending}
                onClick={() => setConfirmingCancel(true)}
                className="self-start text-red-400 hover:bg-red-500/10 active:bg-red-500/20"
              >
                Cancelar agendamento
              </Button>
            ) : (
              <div className="flex flex-col gap-2 rounded-lg border border-brand-white/10 p-3">
                <p className="text-sm text-brand-white/60">
                  O cancelamento pelo site fecha 2 horas antes do horário. Se você não puder vir,
                  avise a barbearia para liberar o horário.
                </p>
                {contactLink && (
                  <a
                    href={contactLink}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="self-start rounded-md border border-brand-gold px-3 py-1.5 text-sm text-brand-gold transition-colors hover:bg-brand-gold/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-gold focus-visible:ring-offset-2 focus-visible:ring-offset-brand-black"
                  >
                    Avisar a barbearia
                  </a>
                )}
              </div>
            )}

            {cancelAppointment.isError && (
              <p role="alert" className="text-sm text-red-400">
                Não conseguimos cancelar agora. Verifique sua conexão e tente de novo.
              </p>
            )}
          </div>
        )}

        {appointment.status === 'Cancelled' && (
          <div className="flex flex-col items-start gap-3 border-t border-brand-white/10 pt-4">
            <p className="text-sm text-brand-white/60">Este agendamento foi cancelado.</p>
            <Link href="/agendar" className={buttonClasses()}>
              Agendar novo horário
            </Link>
          </div>
        )}

        {appointment.status === 'Completed' && !reviewSubmitted && (
          <ReviewForm accessToken={token} onSuccess={() => setReviewSubmitted(true)} />
        )}

        {appointment.status === 'Completed' && reviewSubmitted && (
          <div className="flex flex-col items-start gap-3 border-t border-brand-white/10 pt-4">
            <p className="text-sm text-brand-gold">
              Obrigado pela sua avaliação! Ela ajuda quem ainda não conhece a barbearia.
            </p>
            <Link href="/agendar" className={buttonClasses()}>
              Agendar novo horário
            </Link>
          </div>
        )}
      </div>

      <Modal
        isOpen={confirmingCancel}
        onClose={() => setConfirmingCancel(false)}
        title="Cancelar agendamento?"
      >
        <div className="flex flex-col gap-4">
          <p className="text-sm text-brand-white/70">
            Seu horário de {formatDateTime(appointment.scheduledAt)} com {appointment.barberName}{' '}
            será liberado. Não dá para desfazer.
          </p>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
            <Button variant="ghost" onClick={() => setConfirmingCancel(false)}>
              Manter agendamento
            </Button>
            <Button
              variant="danger"
              isLoading={cancelAppointment.isPending}
              onClick={() => {
                cancelAppointment.mutate(undefined, {
                  onSettled: () => setConfirmingCancel(false),
                })
              }}
            >
              Sim, cancelar
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
