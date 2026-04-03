import { useQuery } from '@tanstack/react-query'
import { servicesApi } from '@/lib/api/services.api'

export function useServices() {
  return useQuery({
    queryKey: ['services'],
    queryFn: () => servicesApi.getAll().then((r) => r.data),
  })
}
