'use client'

import { useState } from 'react'
import { useReinviteCandidates, useReinviteClient } from '@/hooks/useAdminClients'
import { Button } from '@/components/ui/Button'
import { Spinner } from '@/components/ui/Spinner'
import { formatDate } from '@/lib/utils/formatDateTime'
import { formatBrPhone } from '@/lib/utils/phone'
import type { ReinviteCandidate } from '@/types/api.types'

/** Detalhe que a API manda no 404/422 (canal desligado, convite recente), quando houver. */
function errorDetail(error: unknown): string {
  return (
    (error as { response?: { data?: { detail?: string } } })?.response?.data?.detail ??
    'Não foi possível enviar o convite. Tente de novo.'
  )
}

function visits(count: number) {
  return count === 1 ? '1 visita' : `${count} visitas`
}

/**
 * Clientes quase perdidos: a última visita concluída foi há 25–30 dias e não há horário
 * marcado. Um toque manda o convite de volta pelo WhatsApp; o convidado some da lista por
 * 10 dias, então a lista recarrega em vez de marcar a linha.
 */
export function ClientRecurrenceSection() {
  const { data: candidates, isLoading, isError } = useReinviteCandidates()
  const reinvite = useReinviteClient()
  const [invited, setInvited] = useState<string | null>(null)

  function invite(client: ReinviteCandidate) {
    setInvited(null)
    reinvite.mutate(client.clientId, { onSuccess: () => setInvited(client.name) })
  }

  return (
    <section
      aria-labelledby="recorrencia-titulo"
      className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-4"
    >
      <div className="flex flex-col gap-1">
        <h2
          id="recorrencia-titulo"
          className="font-montserrat text-base font-bold text-brand-white sm:text-lg"
        >
          Clientes para chamar de volta
        </h2>
        <p className="text-sm text-brand-white/50">
          Última visita entre 25 e 30 dias atrás e nenhum horário marcado.
        </p>
      </div>

      {/* Sempre montada: região que aparece junto com o texto não é anunciada */}
      <p aria-live="polite" className={invited ? 'mt-3 text-sm text-brand-gold' : undefined}>
        {invited && `Convite enviado para ${invited}.`}
      </p>
      {reinvite.isError && (
        <p role="alert" className="mt-3 text-sm text-red-400">
          {errorDetail(reinvite.error)}
        </p>
      )}

      <div className="mt-3">
        {isLoading ? (
          <div className="flex items-center gap-2 py-2 text-sm text-brand-white/50">
            <Spinner size="sm" /> Carregando clientes…
          </div>
        ) : isError ? (
          <p role="alert" className="py-2 text-sm text-red-400">
            Não foi possível carregar os clientes.
          </p>
        ) : !candidates || candidates.length === 0 ? (
          <p className="py-2 text-sm text-brand-white/30">
            Ninguém sumindo agora. Quem completar 25 dias sem voltar aparece aqui.
          </p>
        ) : (
          <ul>
            {candidates.map((client) => {
              const sending = reinvite.isPending && reinvite.variables === client.clientId
              const nameId = `cliente-${client.clientId}`
              return (
                <li
                  key={client.clientId}
                  className="flex flex-col gap-2 border-b border-brand-white/5 py-3 last:border-0 sm:flex-row sm:items-center sm:gap-3"
                >
                  <div className="min-w-0 flex-1">
                    <p id={nameId} className="truncate text-sm font-medium text-brand-white/80">
                      {client.name}
                    </p>
                    <p className="text-xs text-brand-white/50">
                      {formatBrPhone(client.phone)} · última visita {formatDate(client.lastVisitAt)} ·{' '}
                      {visits(client.visitCount)}
                    </p>
                  </div>
                  <span className="shrink-0 text-sm font-semibold tabular-nums text-brand-gold">
                    há {client.daysSinceLastVisit} dias
                  </span>
                  <Button
                    variant="secondary"
                    size="sm"
                    className="min-h-10 shrink-0"
                    isLoading={sending}
                    disabled={reinvite.isPending}
                    onClick={() => invite(client)}
                    aria-describedby={nameId}
                  >
                    Convidar no WhatsApp
                  </Button>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </section>
  )
}
