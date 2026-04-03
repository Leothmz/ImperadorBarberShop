import type { Metadata } from 'next'
import { BarberAppointmentList } from '@/components/appointments/BarberAppointmentList'

export const metadata: Metadata = {
  title: 'Minha Agenda | O Imperador Barber Shop',
}

export default function BarberDashboardPage() {
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

      <BarberAppointmentList />
    </div>
  )
}
