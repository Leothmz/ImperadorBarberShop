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

// Concluir e cancelar mexem no faturamento do período, então além da lista do
// barbeiro o dashboard financeiro também precisa ser recarregado.
function useAdminAppointmentMutation<TArgs>(
  barberId: string,
  mutationFn: (args: TArgs) => Promise<unknown>
) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'barber', 'appointments', barberId] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'financial'] })
    },
  })
}

export function useAdminCompleteAppointment(barberId: string) {
  return useAdminAppointmentMutation(
    barberId,
    ({ id, paymentMethod }: { id: string; paymentMethod?: PaymentMethod }) =>
      adminApi.completeAppointment(id, paymentMethod)
  )
}

export function useAdminCancelAppointment(barberId: string) {
  return useAdminAppointmentMutation(barberId, ({ id }: { id: string }) =>
    adminApi.cancelAppointment(id)
  )
}
