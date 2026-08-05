'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  useFinancialSummary,
  useFinancialByBarber,
  useFinancialByService,
  useFinancialTimeline,
  useExpenses,
  useCreateExpense,
  useDeleteExpense,
} from '@/hooks/useAdminFinancial'
import { adminApi } from '@/lib/api/admin.api'
import { RevenueChart } from '@/components/ui/RevenueChart'

function today() { return new Date().toISOString().slice(0, 10) }
function weekStart() {
  const d = new Date(); d.setDate(d.getDate() - d.getDay()); return d.toISOString().slice(0, 10)
}
function monthStart() {
  const d = new Date(); d.setDate(1); return d.toISOString().slice(0, 10)
}
function prevPeriodDates(from: string, to: string) {
  const f = new Date(from), t = new Date(to)
  const days = Math.round((t.getTime() - f.getTime()) / 86400000) + 1
  const prevTo = new Date(f); prevTo.setDate(prevTo.getDate() - 1)
  const prevFrom = new Date(prevTo); prevFrom.setDate(prevFrom.getDate() - days + 1)
  return { prevFrom: prevFrom.toISOString().slice(0, 10), prevTo: prevTo.toISOString().slice(0, 10) }
}

const PRESETS = [
  { label: 'Hoje', getDates: () => { const d = today(); return { from: d, to: d } } },
  { label: 'Esta semana', getDates: () => ({ from: weekStart(), to: today() }) },
  { label: 'Este mês', getDates: () => ({ from: monthStart(), to: today() }) },
]

const expenseSchema = z.object({
  amount: z.coerce.number().positive('Valor deve ser positivo'),
  description: z.string().min(1, 'Obrigatório').max(200),
  date: z.string().min(1, 'Obrigatório'),
})

type ExpenseForm = { amount: number; description: string; date: string }

function pct(current: number, previous: number) {
  if (previous === 0) return null
  return Math.round(((current - previous) / previous) * 100)
}

function PctBadge({ value }: { value: number | null }) {
  if (value === null) return null
  const positive = value >= 0
  return (
    <span className={['text-xs font-semibold', positive ? 'text-green-400' : 'text-red-400'].join(' ')}>
      {positive ? '↑' : '↓'} {Math.abs(value)}%
    </span>
  )
}

const CARD = 'rounded-xl border border-brand-white/10 bg-brand-black-soft'

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h2 className="font-montserrat text-base font-bold text-brand-white sm:text-lg">{children}</h2>
}

/** Linha nome + métrica secundária + valor. Substitui a tabela: não estoura em tela estreita. */
function StatRow({ name, meta, value }: { name: string; meta: string; value: string }) {
  return (
    <div className="flex items-center gap-3 border-b border-brand-white/5 py-2.5 last:border-0">
      <span className="min-w-0 flex-1 truncate text-sm text-brand-white/80">{name}</span>
      <span className="shrink-0 text-xs text-brand-white/40">{meta}</span>
      <span className="shrink-0 text-sm font-semibold tabular-nums text-brand-gold">{value}</span>
    </div>
  )
}

export default function DashboardPage() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)
  const [groupBy, setGroupBy] = useState<'day' | 'week' | 'month'>('day')

  const { prevFrom, prevTo } = prevPeriodDates(from, to)

  const { data: summary } = useFinancialSummary(from, to)
  const { data: prevSummary } = useFinancialSummary(prevFrom, prevTo)
  const { data: byBarber } = useFinancialByBarber(from, to)
  const { data: byService } = useFinancialByService(from, to)
  const { data: timeline } = useFinancialTimeline(from, to, groupBy)
  const { data: expenses } = useExpenses(from, to)
  const createExpense = useCreateExpense()
  const deleteExpense = useDeleteExpense()

  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } =
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    useForm<ExpenseForm>({ resolver: zodResolver(expenseSchema) as any, defaultValues: { date: today() } })

  const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
  const fmtDate = (d: string) => new Date(d + 'T00:00:00').toLocaleDateString('pt-BR')

  const summaryCards = [
    {
      label: 'Receita Total',
      value: fmt(summary?.totalRevenue ?? 0),
      compare: pct(summary?.totalRevenue ?? 0, prevSummary?.totalRevenue ?? 0),
      highlight: false,
    },
    {
      label: 'Atendimentos',
      value: String(summary?.totalAppointments ?? 0),
      compare: pct(summary?.totalAppointments ?? 0, prevSummary?.totalAppointments ?? 0),
      highlight: false,
    },
    {
      label: 'Ticket Médio',
      value: fmt(summary?.averageTicket ?? 0),
      compare: pct(summary?.averageTicket ?? 0, prevSummary?.averageTicket ?? 0),
      highlight: false,
    },
    {
      label: 'Despesas',
      value: fmt(summary?.totalExpenses ?? 0),
      compare: pct(summary?.totalExpenses ?? 0, prevSummary?.totalExpenses ?? 0),
      highlight: false,
    },
    {
      label: 'Lucro Líquido',
      value: fmt(summary?.netRevenue ?? 0),
      compare: pct(summary?.netRevenue ?? 0, prevSummary?.netRevenue ?? 0),
      highlight: true,
    },
  ]

  const totalExpensesInPeriod = expenses?.reduce((s, e) => s + e.amount, 0) ?? 0

  async function exportCsv() {
    const blob = await adminApi.exportCsv(from, to)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url; link.download = `relatorio-${from}-${to}.csv`
    document.body.appendChild(link); link.click()
    document.body.removeChild(link); URL.revokeObjectURL(url)
  }

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      {/* Cabeçalho */}
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-montserrat text-2xl font-black text-brand-white">Dashboard Financeiro</h1>
          <p className="mt-1 text-sm text-brand-white/40">
            {fmtDate(from)} até {fmtDate(to)}
          </p>
        </div>
        <button
          onClick={exportCsv}
          className="w-full shrink-0 rounded-lg border border-brand-gold/40 px-4 py-2.5 text-sm font-semibold text-brand-gold transition-colors hover:bg-brand-gold/10 sm:w-auto"
        >
          Exportar CSV
        </button>
      </header>

      {/* Período */}
      <div className={`${CARD} flex flex-col gap-3 p-4`}>
        <div className="flex flex-wrap gap-2">
          {PRESETS.map((p) => {
            const d = p.getDates()
            const active = d.from === from && d.to === to
            return (
              <button
                key={p.label}
                onClick={() => { setFrom(d.from); setTo(d.to) }}
                className={[
                  'flex-1 rounded-lg border px-3 py-2 text-sm transition-colors sm:flex-none',
                  active
                    ? 'border-brand-gold bg-brand-gold/15 font-semibold text-brand-gold'
                    : 'border-brand-white/15 text-brand-white/60 hover:border-brand-gold/40 hover:text-brand-gold',
                ].join(' ')}
              >
                {p.label}
              </button>
            )
          })}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <label className="flex flex-col gap-1">
            <span className="text-xs text-brand-white/40">De</span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
              className="w-full rounded-lg border border-brand-white/20 bg-brand-black px-3 py-2 text-sm text-brand-white focus:border-brand-gold focus:outline-none" />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-xs text-brand-white/40">Até</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
              className="w-full rounded-lg border border-brand-white/20 bg-brand-black px-3 py-2 text-sm text-brand-white focus:border-brand-gold focus:outline-none" />
          </label>
        </div>
      </div>

      {/* Indicadores */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        {summaryCards.map(({ label, value, compare, highlight }) => (
          <div
            key={label}
            className={[
              'rounded-xl border p-4',
              highlight
                ? 'border-brand-gold/40 bg-brand-gold/5'
                : 'border-brand-white/10 bg-brand-black-soft',
            ].join(' ')}
          >
            <p className="text-xs text-brand-white/50">{label}</p>
            <p className="mt-1 font-montserrat text-lg font-black tabular-nums text-brand-gold sm:text-xl">
              {value}
            </p>
            <PctBadge value={compare} />
          </div>
        ))}
      </div>

      {/* Receita ao longo do tempo */}
      <section className={`${CARD} p-4`}>
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <SectionTitle>Receita ao Longo do Tempo</SectionTitle>
          <div className="flex gap-1 rounded-lg border border-brand-white/10 p-1 text-sm">
            {(['day', 'week', 'month'] as const).map((g) => (
              <button
                key={g}
                onClick={() => setGroupBy(g)}
                className={[
                  'flex-1 rounded-md px-3 py-1 transition-colors sm:flex-none',
                  groupBy === g
                    ? 'bg-brand-gold font-semibold text-brand-black'
                    : 'text-brand-white/50 hover:text-brand-white',
                ].join(' ')}
              >
                {g === 'day' ? 'Dia' : g === 'week' ? 'Semana' : 'Mês'}
              </button>
            ))}
          </div>
        </div>
        <RevenueChart data={timeline ?? []} groupBy={groupBy} />
      </section>

      {/* Quebras por barbeiro e serviço */}
      <div className="grid gap-4 lg:grid-cols-2">
        <section className={`${CARD} p-4`}>
          <SectionTitle>Por Barbeiro</SectionTitle>
          <div className="mt-3">
            {byBarber && byBarber.length > 0 ? (
              byBarber.map((row) => (
                <StatRow
                  key={row.barberId}
                  name={row.barberName}
                  meta={`${row.appointments} atend.`}
                  value={fmt(row.revenue)}
                />
              ))
            ) : (
              <p className="py-2 text-sm text-brand-white/30">Nenhum atendimento no período.</p>
            )}
          </div>
        </section>

        <section className={`${CARD} p-4`}>
          <SectionTitle>Por Serviço</SectionTitle>
          <div className="mt-3">
            {byService && byService.length > 0 ? (
              byService.map((row) => (
                <StatRow
                  key={row.serviceId}
                  name={row.serviceName}
                  meta={`${row.count}x`}
                  value={fmt(row.revenue)}
                />
              ))
            ) : (
              <p className="py-2 text-sm text-brand-white/30">Nenhum serviço vendido no período.</p>
            )}
          </div>
        </section>
      </div>

      {/* Despesas */}
      <section className={`${CARD} p-4`}>
        <div className="flex items-center justify-between gap-3">
          <SectionTitle>Despesas</SectionTitle>
          <span className="text-sm font-semibold tabular-nums text-brand-gold">
            {fmt(totalExpensesInPeriod)}
          </span>
        </div>

        <form
          onSubmit={handleSubmit(async (data) => {
            await createExpense.mutateAsync({ amount: data.amount, description: data.description, date: data.date })
            reset({ date: today() })
          })}
          className="mt-4 grid grid-cols-2 gap-3"
        >
          <label className="flex flex-col gap-1">
            <span className="text-xs text-brand-white/50">Valor (R$)</span>
            <input
              type="number"
              step="0.01"
              min="0.01"
              placeholder="0,00"
              {...register('amount')}
              className="w-full rounded-lg border border-brand-white/20 bg-brand-black px-3 py-2 text-sm text-brand-white focus:border-brand-gold focus:outline-none"
            />
            {errors.amount && <span className="text-xs text-brand-gold/70">{errors.amount.message}</span>}
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-xs text-brand-white/50">Data</span>
            <input
              type="date"
              {...register('date')}
              className="w-full rounded-lg border border-brand-white/20 bg-brand-black px-3 py-2 text-sm text-brand-white focus:border-brand-gold focus:outline-none"
            />
          </label>
          <label className="col-span-2 flex flex-col gap-1">
            <span className="text-xs text-brand-white/50">Descrição</span>
            <input
              type="text"
              placeholder="Ex: Produto, aluguel..."
              maxLength={200}
              {...register('description')}
              className="w-full rounded-lg border border-brand-white/20 bg-brand-black px-3 py-2 text-sm text-brand-white focus:border-brand-gold focus:outline-none"
            />
            {errors.description && <span className="text-xs text-brand-gold/70">{errors.description.message}</span>}
          </label>
          <button
            type="submit"
            disabled={isSubmitting}
            className="col-span-2 rounded-lg bg-brand-gold px-4 py-2.5 text-sm font-semibold text-brand-black transition-colors hover:bg-brand-gold-light disabled:opacity-50 sm:col-span-1"
          >
            {isSubmitting ? 'Adicionando...' : 'Adicionar'}
          </button>
        </form>

        <div className="mt-4">
          {expenses && expenses.length > 0 ? (
            expenses.map((e) => (
              <div
                key={e.id}
                className="flex items-center gap-3 border-b border-brand-white/5 py-2.5 last:border-0"
              >
                <span className="min-w-0 flex-1 truncate text-sm text-brand-white/70">{e.description}</span>
                <span className="shrink-0 text-xs text-brand-white/40">{fmtDate(e.date)}</span>
                <span className="shrink-0 text-sm font-semibold tabular-nums text-brand-gold">{fmt(e.amount)}</span>
                <button
                  onClick={() => {
                    if (!window.confirm(`Excluir despesa "${e.description}"?`)) return
                    deleteExpense.mutate(e.id)
                  }}
                  className="shrink-0 px-1 text-brand-white/30 transition-colors hover:text-brand-white/70"
                  aria-label="Excluir despesa"
                >
                  ✕
                </button>
              </div>
            ))
          ) : (
            <p className="text-sm text-brand-white/30">Nenhuma despesa registrada no período.</p>
          )}
        </div>
      </section>
    </div>
  )
}
