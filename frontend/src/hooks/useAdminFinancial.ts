import { useQuery } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'

export function useFinancialSummary(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'summary', from, to],
    queryFn: () => adminApi.getSummary(from, to),
    enabled: !!from && !!to,
  })
}

export function useFinancialByBarber(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'barber', from, to],
    queryFn: () => adminApi.getByBarber(from, to),
    enabled: !!from && !!to,
  })
}

export function useFinancialByService(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'service', from, to],
    queryFn: () => adminApi.getByService(from, to),
    enabled: !!from && !!to,
  })
}
