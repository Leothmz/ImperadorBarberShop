'use client'

import { useParams } from 'next/navigation'
import { ManageAppointmentView } from './ManageAppointmentView'

export default function ManageAppointmentPage() {
  const params = useParams<{ token: string }>()
  return <ManageAppointmentView token={params.token} />
}
