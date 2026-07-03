import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { CreateExpensePayload } from '@/types/api.types'

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

export function useFinancialTimeline(from: string, to: string, groupBy: 'day' | 'week' | 'month') {
  return useQuery({
    queryKey: ['admin', 'financial', 'timeline', from, to, groupBy],
    queryFn: () => adminApi.getTimeline(from, to, groupBy),
    enabled: !!from && !!to,
  })
}

export function useExpenses(from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'financial', 'expenses', from, to],
    queryFn: () => adminApi.getExpenses(from, to),
    enabled: !!from && !!to,
  })
}

export function useCreateExpense() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateExpensePayload) => adminApi.createExpense(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'expenses'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'summary'] })
    },
  })
}

export function useDeleteExpense() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.deleteExpense(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'expenses'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial', 'summary'] })
    },
  })
}
