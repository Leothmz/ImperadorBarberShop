import type { AppointmentStatus } from '@/types/api.types'
import { getStatusConfig } from '@/lib/utils/statusConfig'

interface BadgeProps {
  status: AppointmentStatus
  className?: string
}

export function Badge({ status, className = '' }: BadgeProps) {
  const config = getStatusConfig(status)
  return (
    <span
      className={[
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold',
        config.color,
        config.bgColor,
        className,
      ].join(' ')}
    >
      {config.label}
    </span>
  )
}
