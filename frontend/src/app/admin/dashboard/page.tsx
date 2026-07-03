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

// eslint-disable-next-line @typescript-eslint/no-explicit-any
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

  const summaryCards = [
    {
      label: 'Receita Total',
      value: fmt(summary?.totalRevenue ?? 0),
      compare: pct(summary?.totalRevenue ?? 0, prevSummary?.totalRevenue ?? 0),
    },
    {
      label: 'Atendimentos',
      value: String(summary?.totalAppointments ?? 0),
      compare: pct(summary?.totalAppointments ?? 0, prevSummary?.totalAppointments ?? 0),
    },
    {
      label: 'Ticket Médio',
      value: fmt(summary?.averageTicket ?? 0),
      compare: pct(summary?.averageTicket ?? 0, prevSummary?.averageTicket ?? 0),
    },
    {
      label: 'Despesas',
      value: fmt(summary?.totalExpenses ?? 0),
      compare: pct(summary?.totalExpenses ?? 0, prevSummary?.totalExpenses ?? 0),
      invertColor: true,
    },
    {
      label: 'Lucro Líquido',
      value: fmt(summary?.netRevenue ?? 0),
      compare: pct(summary?.netRevenue ?? 0, prevSummary?.netRevenue ?? 0),
    },
  ]

  const totalExpensesInPeriod = expenses?.reduce((s, e) => s + e.amount, 0) ?? 0

  return (
    <div className="space-y-8">
      <h1 className="font-montserrat text-2xl font-black text-brand-white">Dashboard Financeiro</h1>

      {/* Period selector */}
      <div className="flex flex-wrap gap-3 items-center">
        {PRESETS.map((p) => (
          <button
            key={p.label}
            onClick={() => { const d = p.getDates(); setFrom(d.from); setTo(d.to) }}
            className="px-4 py-2 rounded-lg border border-brand-gold/30 text-sm text-brand-gold hover:bg-brand-gold/10 transition-colors"
          >
            {p.label}
          </button>
        ))}
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <span className="text-brand-white/50">até</span>
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm" />
        <button
          onClick={async () => {
            const blob = await adminApi.exportCsv(from, to)
            const url = URL.createObjectURL(blob)
            const link = document.createElement('a')
            link.href = url; link.download = `relatorio-${from}-${to}.csv`
            document.body.appendChild(link); link.click()
            document.body.removeChild(link); URL.revokeObjectURL(url)
          }}
          className="ml-auto px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors"
        >
          Exportar CSV
        </button>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {summaryCards.map(({ label, value, compare }) => (
          <div key={label} className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-5">
            <p className="text-xs text-brand-white/50">{label}</p>
            <p className="font-montserrat text-xl font-black text-brand-gold mt-1">{value}</p>
            <PctBadge value={compare} />
          </div>
        ))}
      </div>

      {/* Revenue chart */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-montserrat text-lg font-bold text-brand-white">Receita ao Longo do Tempo</h2>
          <div className="flex gap-1 text-sm">
            {(['day', 'week', 'month'] as const).map((g) => (
              <button
                key={g}
                onClick={() => setGroupBy(g)}
                className={[
                  'px-3 py-1 rounded-lg transition-colors',
                  groupBy === g
                    ? 'bg-brand-gold text-brand-black font-semibold'
                    : 'text-brand-white/50 hover:text-brand-white',
                ].join(' ')}
              >
                {g === 'day' ? 'Dia' : g === 'week' ? 'Semana' : 'Mês'}
              </button>
            ))}
          </div>
        </div>
        <div className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-4">
          <RevenueChart data={timeline ?? []} groupBy={groupBy} />
        </div>
      </section>

      {/* By Barber */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Barbeiro</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 text-left text-brand-white/40">
              <th className="pb-2">Barbeiro</th>
              <th className="pb-2">Atendimentos</th>
              <th className="pb-2">Receita</th>
            </tr>
          </thead>
          <tbody>
            {byBarber?.map((row) => (
              <tr key={row.barberId} className="border-b border-brand-white/5">
                <td className="py-2">{row.barberName}</td>
                <td className="py-2">{row.appointments}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* By Service */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Por Serviço</h2>
        <table className="w-full text-sm text-brand-white/80">
          <thead>
            <tr className="border-b border-brand-white/10 text-left text-brand-white/40">
              <th className="pb-2">Serviço</th>
              <th className="pb-2">Vendas</th>
              <th className="pb-2">Receita</th>
            </tr>
          </thead>
          <tbody>
            {byService?.map((row) => (
              <tr key={row.serviceId} className="border-b border-brand-white/5">
                <td className="py-2">{row.serviceName}</td>
                <td className="py-2">{row.count}</td>
                <td className="py-2 text-brand-gold">{fmt(row.revenue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Expenses section */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">Despesas</h2>

        {/* Add expense form */}
        <form
          onSubmit={handleSubmit(async (data) => {
            await createExpense.mutateAsync({ amount: data.amount, description: data.description, date: data.date })
            reset({ date: today() })
          })}
          className="flex flex-wrap gap-3 mb-6 items-end"
        >
          <div className="flex flex-col gap-1">
            <label className="text-xs text-brand-white/50">Valor (R$)</label>
            <input
              type="number"
              step="0.01"
              min="0.01"
              placeholder="0,00"
              {...register('amount')}
              className="w-28 bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
            {errors.amount && <span className="text-xs text-brand-gold/70">{errors.amount.message}</span>}
          </div>
          <div className="flex flex-col gap-1 flex-1 min-w-[160px]">
            <label className="text-xs text-brand-white/50">Descrição</label>
            <input
              type="text"
              placeholder="Ex: Produto, aluguel..."
              maxLength={200}
              {...register('description')}
              className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
            {errors.description && <span className="text-xs text-brand-gold/70">{errors.description.message}</span>}
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs text-brand-white/50">Data</label>
            <input
              type="date"
              {...register('date')}
              className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm focus:border-brand-gold focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={isSubmitting}
            className="px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors disabled:opacity-50"
          >
            {isSubmitting ? 'Adicionando...' : 'Adicionar'}
          </button>
        </form>

        {/* Expense list */}
        {expenses && expenses.length > 0 ? (
          <div className="flex flex-col gap-1">
            {expenses.map((e) => (
              <div
                key={e.id}
                className="flex items-center gap-4 rounded-lg border border-brand-white/5 bg-brand-black-soft px-4 py-2 text-sm"
              >
                <span className="text-brand-white/60 flex-1">{e.description}</span>
                <span className="text-brand-white/40 text-xs">{new Date(e.date + 'T00:00:00').toLocaleDateString('pt-BR')}</span>
                <span className="font-semibold text-brand-gold">{fmt(e.amount)}</span>
                <button
                  onClick={() => {
                    if (!window.confirm(`Excluir despesa "${e.description}"?`)) return
                    deleteExpense.mutate(e.id)
                  }}
                  className="text-brand-white/30 hover:text-brand-white/70 transition-colors px-1"
                  aria-label="Excluir despesa"
                >
                  ✕
                </button>
              </div>
            ))}
            <div className="flex justify-end pt-2 text-sm font-semibold text-brand-white/60">
              Total: <span className="ml-2 text-brand-gold">{fmt(totalExpensesInPeriod)}</span>
            </div>
          </div>
        ) : (
          <p className="text-brand-white/30 text-sm">Nenhuma despesa registrada no período.</p>
        )}
      </section>
    </div>
  )
}
