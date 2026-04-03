import type { Metadata } from 'next'
import Link from 'next/link'
import { ClientAppointmentList } from '@/components/appointments/ClientAppointmentList'
import { Button } from '@/components/ui/Button'

export const metadata: Metadata = {
  title: 'Meus Agendamentos | O Imperador Barber Shop',
}

export default function ClientDashboardPage() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="font-montserrat text-2xl font-black text-brand-white">
            Meus Agendamentos
          </h1>
          <p className="mt-1 text-sm text-brand-white/50">
            Gerencie seus agendamentos e avalie os serviços concluídos
          </p>
        </div>
        <Link href="/client/book">
          <Button size="sm">Novo agendamento</Button>
        </Link>
      </div>

      <ClientAppointmentList />
    </div>
  )
}
