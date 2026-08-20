'use client'

import { Suspense } from 'react'
import { useParams, useSearchParams } from 'next/navigation'
import { Spinner } from '@/components/ui/Spinner'
import { ManageAppointmentView } from './ManageAppointmentView'

function ManageAppointment() {
  const params = useParams<{ token: string }>()
  const searchParams = useSearchParams()
  return (
    <ManageAppointmentView token={params.token} isNew={searchParams.get('novo') === '1'} />
  )
}

export default function ManageAppointmentPage() {
  // useSearchParams exige limite de Suspense para a página poder ser pré-renderizada.
  return (
    <Suspense
      fallback={
        <div className="flex h-64 items-center justify-center">
          <Spinner size="lg" />
        </div>
      }
    >
      <ManageAppointment />
    </Suspense>
  )
}
