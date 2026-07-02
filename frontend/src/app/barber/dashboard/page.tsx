'use client'

import { useState } from 'react'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'
import BlocksTab from './BlocksTab'

type Tab = 'agenda' | 'bloqueios'

export default function BarberDashboardPage() {
  const [activeTab, setActiveTab] = useState<Tab>('agenda')

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
      <div className="mb-8">
        <h1 className="font-montserrat text-2xl font-black text-brand-white">
          Minha Agenda
        </h1>
        <p className="mt-1 text-sm text-brand-white/50">
          Gerencie seus agendamentos — aceite, recuse ou conclua atendimentos
        </p>
      </div>

      {/* Tab Navigation */}
      <div className="mb-6 flex gap-1 border-b border-brand-white/10">
        <button
          onClick={() => setActiveTab('agenda')}
          className={`px-4 py-2 text-sm font-medium transition-colors ${
            activeTab === 'agenda'
              ? 'border-b-2 border-brand-gold text-brand-gold'
              : 'text-brand-white/50 hover:text-brand-white'
          }`}
        >
          Agendamentos
        </button>
        <button
          onClick={() => setActiveTab('bloqueios')}
          className={`px-4 py-2 text-sm font-medium transition-colors ${
            activeTab === 'bloqueios'
              ? 'border-b-2 border-brand-gold text-brand-gold'
              : 'text-brand-white/50 hover:text-brand-white'
          }`}
        >
          Bloqueios
        </button>
      </div>

      {activeTab === 'agenda' && <BarberAppointmentList />}
      {activeTab === 'bloqueios' && <BlocksTab />}
    </div>
  )
}
