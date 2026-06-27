import type { AppointmentStatus } from '@/types/api.types'

export interface StatusConfig {
  label: string
  color: string // Tailwind CSS class
  bgColor: string
}

export const statusConfig: Record<AppointmentStatus, StatusConfig> = {
  Accepted: {
    label: 'Confirmado',
    color: 'text-green-400',
    bgColor: 'bg-green-400/20',
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
