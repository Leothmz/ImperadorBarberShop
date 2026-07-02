'use client'

import { useState } from 'react'
import {
  useFinancialSummary,
  useFinancialByBarber,
  useFinancialByService,
} from '@/hooks/useAdminFinancial'
import { adminApi } from '@/lib/api/admin.api'

function today() {
  return new Date().toISOString().slice(0, 10)
}

function weekStart() {
  const d = new Date()
  d.setDate(d.getDate() - d.getDay())
  return d.toISOString().slice(0, 10)
}

function monthStart() {
  const d = new Date()
  d.setDate(1)
  return d.toISOString().slice(0, 10)
}

const PRESETS = [
  {
    label: 'Hoje',
    getDates: () => { const d = today(); return { from: d, to: d } },
  },
  {
    label: 'Esta semana',
    getDates: () => ({ from: weekStart(), to: today() }),
  },
  {
    label: 'Este mês',
    getDates: () => ({ from: monthStart(), to: today() }),
  },
]

export default function DashboardPage() {
  const [from, setFrom] = useState(monthStart)
  const [to, setTo] = useState(today)

  const { data: summary } = useFinancialSummary(from, to)
  const { data: byBarber } = useFinancialByBarber(from, to)
  const { data: byService } = useFinancialByService(from, to)

  const fmt = (n: number) =>
    n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

  return (
    <div className="space-y-8">
      <h1 className="font-montserrat text-2xl font-black text-brand-white">
        Dashboard Financeiro
      </h1>

      {/* Period selector */}
      <div className="flex flex-wrap gap-3 items-center">
        {PRESETS.map((p) => (
          <button
            key={p.label}
            onClick={() => {
              const d = p.getDates()
              setFrom(d.from)
              setTo(d.to)
            }}
            className="px-4 py-2 rounded-lg border border-brand-gold/30 text-sm text-brand-gold hover:bg-brand-gold/10 transition-colors"
          >
            {p.label}
          </button>
        ))}
        <input
          type="date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm"
        />
        <span className="text-brand-white/50">até</span>
        <input
          type="date"
          value={to}
          onChange={(e) => setTo(e.target.value)}
          className="bg-brand-black-soft border border-brand-white/20 text-brand-white rounded-lg px-3 py-2 text-sm"
        />
        <a
          href={`${process.env.NEXT_PUBLIC_API_URL}${adminApi.exportCsvUrl(from, to)}`}
          download
          className="ml-auto px-4 py-2 rounded-lg bg-brand-gold text-brand-black text-sm font-semibold hover:bg-brand-gold-light transition-colors"
        >
          Exportar CSV
        </a>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[
          { label: 'Receita Total', value: fmt(summary?.totalRevenue ?? 0) },
          { label: 'Atendimentos', value: String(summary?.totalAppointments ?? 0) },
          { label: 'Ticket Médio', value: fmt(summary?.averageTicket ?? 0) },
        ].map(({ label, value }) => (
          <div
            key={label}
            className="rounded-xl border border-brand-white/10 bg-brand-black-soft p-6"
          >
            <p className="text-sm text-brand-white/50">{label}</p>
            <p className="font-montserrat text-2xl font-black text-brand-gold mt-1">
              {value}
            </p>
          </div>
        ))}
      </div>

      {/* By Barber table */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">
          Por Barbeiro
        </h2>
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

      {/* By Service table */}
      <section>
        <h2 className="font-montserrat text-lg font-bold text-brand-white mb-4">
          Por Serviço
        </h2>
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
    </div>
  )
}
