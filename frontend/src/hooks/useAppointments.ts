import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { appointmentsApi } from '@/lib/api/appointments.api'
import type { CreateAppointmentPayload } from '@/types/api.types'

export function useClientAppointments() {
  return useQuery({
    queryKey: ['appointments', 'mine'],
    queryFn: () => appointmentsApi.getMine().then((r) => r.data),
  })
}

export function useBarberAppointments() {
  return useQuery({
    queryKey: ['appointments', 'barber'],
    queryFn: () => appointmentsApi.getBarberAppointments().then((r) => r.data),
  })
}

export function useCreateAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateAppointmentPayload) =>
      appointmentsApi.create(payload).then((r) => r.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments'] })
    },
  })
}

export function useCancelAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.cancel(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['appointments'] })
    },
  })
}

export function useAcceptAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.accept(id).then((r) => r.data),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Accepted' } : a))
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

export function useRejectAppointment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => appointmentsApi.reject(id).then((r) => r.data),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['appointments', 'barber'] })
      const previous = queryClient.getQueryData(['appointments', 'barber'])
      queryClient.setQueryData(['appointments', 'barber'], (old: unknown) => {
        if (!Array.isArray(old)) return old
        return old.map((a) => (a.id === id ? { ...a, status: 'Rejected' } : a))
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
