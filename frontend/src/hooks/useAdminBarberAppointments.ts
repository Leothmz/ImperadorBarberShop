import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/lib/api/admin.api'
import type { AppointmentPayment } from '@/types/api.types'

export function useAdminBarberAppointments(barberId: string) {
  return useQuery({
    queryKey: ['admin', 'barber', 'appointments', barberId],
    queryFn: () => adminApi.getBarberAppointments(barberId),
    enabled: !!barberId,
  })
}

// Concluir, cancelar e registrar pagamento mexem no faturamento do período (o plano
// entra pelo valor cobrado), então além da lista do barbeiro o dashboard financeiro
// também precisa ser recarregado.
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

export function useAdminUpdateAppointmentPayment(barberId: string) {
  return useAdminAppointmentMutation(
    barberId,
    ({ id, payment }: { id: string; payment: AppointmentPayment }) =>
      adminApi.updateAppointmentPayment(id, payment)
  )
}

export function useAdminCompleteAppointment(barberId: string) {
  return useAdminAppointmentMutation(
    barberId,
    ({ id, payment }: { id: string; payment?: AppointmentPayment }) =>
      adminApi.completeAppointment(id, payment)
  )
}

export function useAdminCancelAppointment(barberId: string) {
  return useAdminAppointmentMutation(barberId, ({ id }: { id: string }) =>
    adminApi.cancelAppointment(id)
  )
}
