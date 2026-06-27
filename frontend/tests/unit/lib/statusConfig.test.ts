import { describe, it, expect } from 'vitest'
import { getStatusConfig, statusConfig } from '@/lib/utils/statusConfig'
import type { AppointmentStatus } from '@/types/api.types'

describe('statusConfig', () => {
  const statuses: AppointmentStatus[] = ['Accepted', 'Cancelled', 'Completed']

  it('has entries for all appointment statuses', () => {
    statuses.forEach((status) => {
      expect(statusConfig[status]).toBeDefined()
    })
  })

  it('each status config has a label, color, and bgColor', () => {
    statuses.forEach((status) => {
      const config = statusConfig[status]
      expect(config.label).toBeTruthy()
      expect(config.color).toBeTruthy()
      expect(config.bgColor).toBeTruthy()
    })
  })

  it('returns correct label for Accepted', () => {
    expect(statusConfig.Accepted.label).toBe('Confirmado')
  })

  it('returns correct label for Cancelled', () => {
    expect(statusConfig.Cancelled.label).toBe('Cancelado')
  })

  it('returns correct label for Completed', () => {
    expect(statusConfig.Completed.label).toBe('Concluído')
  })
})

describe('getStatusConfig', () => {
  it('returns the correct config for a given status', () => {
    const config = getStatusConfig('Accepted')
    expect(config.label).toBe('Confirmado')
  })

  it('is equivalent to direct statusConfig lookup', () => {
    const statuses: AppointmentStatus[] = ['Accepted', 'Cancelled', 'Completed']
    statuses.forEach((status) => {
      expect(getStatusConfig(status)).toEqual(statusConfig[status])
    })
  })
})
