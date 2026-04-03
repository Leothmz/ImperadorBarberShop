import type { AppointmentStatus } from '@/types/api.types'

export interface StatusConfig {
  label: string
  color: string // Tailwind CSS class
  bgColor: string
}

export const statusConfig: Record<AppointmentStatus, StatusConfig> = {
  Pending: {
    label: 'Pendente',
    color: 'text-yellow-400',
    bgColor: 'bg-yellow-400/20',
  },
  Accepted: {
    label: 'Aceito',
    color: 'text-green-400',
    bgColor: 'bg-green-400/20',
  },
  Rejected: {
    label: 'Recusado',
    color: 'text-red-400',
    bgColor: 'bg-red-400/20',
  },
  Cancelled: {
    label: 'Cancelado',
    color: 'text-gray-400',
    bgColor: 'bg-gray-400/20',
  },
  Completed: {
    label: 'Concluído',
    color: 'text-brand-gold',
    bgColor: 'bg-brand-gold/20',
  },
}

export function getStatusConfig(status: AppointmentStatus): StatusConfig {
  return statusConfig[status]
}
