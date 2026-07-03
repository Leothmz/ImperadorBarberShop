'use client'

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import type { FinancialTimelineItem } from '@/types/api.types'

interface RevenueChartProps {
  data: FinancialTimelineItem[]
  groupBy: 'day' | 'week' | 'month'
}

function formatPeriod(period: string, groupBy: 'day' | 'week' | 'month') {
  const date = new Date(period + 'T00:00:00')
  if (groupBy === 'month') return date.toLocaleDateString('pt-BR', { month: 'short', year: '2-digit' })
  if (groupBy === 'week') return `Sem ${date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })}`
  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
}

function formatCurrency(value: number) {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export function RevenueChart({ data, groupBy }: RevenueChartProps) {
  if (data.length === 0) {
    return (
      <div className="flex h-48 items-center justify-center text-brand-white/30 text-sm">
        Nenhum dado para o período.
      </div>
    )
  }

  const chartData = data.map((item) => ({
    period: formatPeriod(item.period, groupBy),
    receita: item.revenue,
    atendimentos: item.appointments,
  }))

  return (
    <ResponsiveContainer width="100%" height={220}>
      <BarChart data={chartData} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(245,245,245,0.08)" />
        <XAxis
          dataKey="period"
          tick={{ fill: 'rgba(245,245,245,0.4)', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          tickFormatter={(v) => `R$${v}`}
          tick={{ fill: 'rgba(245,245,245,0.4)', fontSize: 11 }}
          axisLine={false}
          tickLine={false}
          width={56}
        />
        <Tooltip
          contentStyle={{ background: '#1A1A1A', border: '1px solid rgba(201,168,76,0.3)', borderRadius: 8 }}
          labelStyle={{ color: '#F5F5F5', marginBottom: 4 }}
          formatter={(value) => [formatCurrency(Number(value)), 'Receita']}
        />
        <Bar dataKey="receita" fill="#C9A84C" radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  )
}
