import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { PaymentMethod } from '@/types/api.types'

export function useAdminBarberAppointments(barberId: string) {
  return useQuery({
    queryKey: ['admin', 'barber', 'appointments', barberId],
    queryFn: () => adminApi.getBarberAppointments(barberId),
    enabled: !!barberId,
  })
}

export function useAdminUpdateAppointmentPayment(barberId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, paymentMethod }: { id: string; paymentMethod: PaymentMethod }) =>
      adminApi.updateAppointmentPayment(id, paymentMethod),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'barber', 'appointments', barberId] })
    },
  })
}
