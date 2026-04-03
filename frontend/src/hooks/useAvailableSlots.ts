import { useQuery } from '@tanstack/react-query'
import { barbersApi } from '@/lib/api/barbers.api'

interface UseSlotsOptions {
  barberId: string
  date: string // YYYY-MM-DD
  serviceIds: string[]
}

export function useAvailableSlots({ barberId, date, serviceIds }: UseSlotsOptions) {
  return useQuery({
    queryKey: ['slots', barberId, date, serviceIds],
    queryFn: () => barbersApi.getSlots(barberId, date, serviceIds).then((r) => r.data),
    enabled: !!barberId && !!date && serviceIds.length > 0,
  })
}
