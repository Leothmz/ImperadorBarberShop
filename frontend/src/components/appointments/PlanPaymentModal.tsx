'use client'

import { useState, type FormEvent } from 'react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { formatCurrency } from '@/lib/utils/formatDateTime'
import { PAYMENT_ICONS, PLAN_TENDERS, parseChargedAmount } from '@/lib/utils/payment'
import type { AppointmentPayment, PlanKind, PlanTender } from '@/types/api.types'

/** Concluir um atendimento confirmado já com o plano, ou registrar o plano num concluído. */
export type PlanPaymentAction = 'complete' | 'register'

const CONFIRM_LABELS: Record<PlanPaymentAction, string> = {
  complete: 'Concluir atendimento',
  register: 'Registrar pagamento',
}

type Step = 'kind' | PlanKind

const TITLES: Record<Step, string> = {
  kind: 'Plano',
  Pagamento: 'Pagamento do plano',
  Recorrencia: 'Recorrência do plano',
}

const FALLBACK_ERROR = 'Não foi possível salvar o pagamento do plano. Tente de novo.'

interface PlanPaymentModalProps {
  clientName: string
  action: PlanPaymentAction
  onClose: () => void
  /** Envia o pagamento. Se rejeitar, o modal continua aberto e mostra o erro. */
  onConfirm: (payment: AppointmentPayment) => Promise<unknown>
}

/**
 * O fluxo Plano dos dois painéis: primeiro Pagamento ou Recorrência; no pagamento, o valor
 * cobrado e a forma (Pix, Dinheiro ou Cartão); na recorrência, só a confirmação do R$ 0.
 * Monte só quando for abrir: cada abertura começa do primeiro passo.
 */
export function PlanPaymentModal({ clientName, action, onClose, onConfirm }: PlanPaymentModalProps) {
  const [step, setStep] = useState<Step>('kind')
  const [amountText, setAmountText] = useState('')
  const [tender, setTender] = useState<PlanTender | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const amount = parseChargedAmount(amountText)

  async function submit(payment: AppointmentPayment) {
    setSubmitting(true)
    setError(null)
    try {
      await onConfirm(payment)
    } catch (err) {
      setError(firstApiError(err) ?? FALLBACK_ERROR)
      setSubmitting(false)
    }
  }

  function submitPayment(e: FormEvent) {
    e.preventDefault()
    if (amount.amount === null || tender === null) return
    submit({ paymentMethod: 'Plano', planKind: 'Pagamento', planTender: tender, chargedAmount: amount.amount })
  }

  function back() {
    setStep('kind')
    setError(null)
  }

  return (
    <Modal isOpen onClose={onClose} title={TITLES[step]}>
      {step === 'kind' && (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-brand-white/70">
            Atendimento de <strong className="text-brand-white">{clientName}</strong>. É pagamento do plano ou recorrência?
          </p>
          <div className="grid gap-3 sm:grid-cols-2">
            <KindOption
              label="Pagamento"
              description="O cliente paga o plano nesta visita."
              onClick={() => setStep('Pagamento')}
            />
            <KindOption
              label="Recorrência"
              description="Visita coberta por um plano já pago."
              onClick={() => setStep('Recorrencia')}
            />
          </div>
          <div className="flex justify-end">
            <Button variant="secondary" size="sm" onClick={onClose}>
              Cancelar
            </Button>
          </div>
        </div>
      )}

      {step === 'Pagamento' && (
        <form onSubmit={submitPayment} className="flex flex-col gap-4" noValidate>
          <Input
            id="plan-charged-amount"
            label="Valor cobrado (R$)"
            inputMode="decimal"
            autoComplete="off"
            placeholder="0,00"
            value={amountText}
            onChange={(e) => setAmountText(e.target.value)}
            error={amount.error ?? undefined}
            hint="O valor que entra no caixa por este atendimento."
            autoFocus
          />
          <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium text-brand-white/80">Forma de pagamento</legend>
            <div className="flex flex-wrap gap-2">
              {PLAN_TENDERS.map((t) => (
                <button
                  key={t}
                  type="button"
                  aria-pressed={tender === t}
                  onClick={() => setTender(t)}
                  className={[
                    'rounded-lg border px-3 py-1.5 text-sm transition-colors',
                    tender === t
                      ? 'border-brand-gold bg-brand-gold/20 text-brand-gold'
                      : 'border-brand-white/20 text-brand-white/60 hover:border-brand-gold/50',
                  ].join(' ')}
                >
                  {PAYMENT_ICONS[t]} {t}
                </button>
              ))}
            </div>
          </fieldset>
          {error && <p role="alert" className="text-sm text-red-400">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" size="sm" onClick={back} disabled={submitting}>
              Voltar
            </Button>
            <Button type="submit" size="sm" isLoading={submitting} disabled={amount.amount === null || tender === null}>
              {CONFIRM_LABELS[action]}
            </Button>
          </div>
        </form>
      )}

      {step === 'Recorrencia' && (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-brand-white/70">
            Nada é cobrado nesta visita. O atendimento entra no caixa como{' '}
            <strong className="text-brand-gold">{formatCurrency(0)}</strong>, sem forma de pagamento, e os
            serviços continuam registrados.
          </p>
          {error && <p role="alert" className="text-sm text-red-400">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" size="sm" onClick={back} disabled={submitting}>
              Voltar
            </Button>
            <Button
              size="sm"
              isLoading={submitting}
              onClick={() => submit({ paymentMethod: 'Plano', planKind: 'Recorrencia' })}
            >
              {CONFIRM_LABELS[action]}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  )
}

function KindOption({ label, description, onClick }: { label: string; description: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex flex-col gap-1 rounded-lg border border-brand-white/15 p-4 text-left transition-colors hover:border-brand-gold hover:bg-brand-gold/5"
    >
      <span className="font-montserrat font-semibold text-brand-white">{label}</span>
      <span className="text-xs text-brand-white/50">{description}</span>
    </button>
  )
}

/** A primeira mensagem de validação que a API devolveu (400), quando houver. */
function firstApiError(err: unknown): string | null {
  const errors = (err as { response?: { data?: { errors?: Record<string, string[]> } } })?.response?.data?.errors
  return errors ? Object.values(errors).flat()[0] ?? null : null
}
