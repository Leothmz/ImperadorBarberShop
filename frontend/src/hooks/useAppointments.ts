import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appointmentsApi } from '@/lib/api/appointments.api'
import type { CreateAppointmentPayload, CreateReviewByTokenPayload } from '@/types/api.types'

export function useCreateAppointment() {
  return useMutation({
    mutationFn: (payload: CreateAppointmentPayload) =>
      appointmentsApi.create(payload).then((r) => r.data),
  })
}

export function useAppointmentByToken(token: string) {
  return useQuery({
    queryKey: ['appointments', 'manage', token],
    queryFn: () => appointmentsApi.getByToken(token).then((r) => r.data),
    enabled: !!token,
  })
}

export function useCancelAppointmentByToken(token: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => appointmentsApi.cancelByToken(token),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'manage', token] })
    },
  })
}

export function useCreateReviewByToken(token: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateReviewByTokenPayload) =>
      appointmentsApi.reviewByToken(token, payload).then((r) => r.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'manage', token] })
    },
  })
}

export function useBarberAppointments() {
  return useQuery({
    queryKey: ['appointments', 'barber'],
    queryFn: () => appointmentsApi.getBarberAppointments().then((r) => r.data),
  })
}

export function useCancelAppointmentByBarber() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.cancelByBarber(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Cancelled' } : a))
      })
      return { previous }
    },
    onError: (_err, _id, context) => {
      queryClient.setQueryData(['appointments', 'barber'], context?.previous)
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}

export function useCompleteAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.complete(id).then((r) => r.data),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Completed' } : a))
      })
      return { previous }
    },
    onError: (_err, _id, context) => {
      queryClient.setQueryData(['appointments', 'barber'], context?.previous)
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments', 'barber'] })
    },
  })
}
