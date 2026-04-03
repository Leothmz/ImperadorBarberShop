import { useQuery } from '@tanstack/react-query'
import { barbersApi } from '@/lib/api/barbers.api'

export function useBarbers() {
  return useQuery({
    queryKey: ['barbers'],
    queryFn: () => barbersApi.getAll().then((r) => r.data),
  })
}

export function useBarber(id: string) {
  return useQuery({
    queryKey: ['barbers', id],
    queryFn: () => barbersApi.getById(id).then((r) => r.data),
    enabled: !!id,
  })
}

export function useBarberReviews(id: string) {
  return useQuery({
    queryKey: ['barbers', id, 'reviews'],
    queryFn: () => barbersApi.getReviews(id).then((r) => r.data),
    enabled: !!id,
  })
}
