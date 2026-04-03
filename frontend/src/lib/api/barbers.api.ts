import apiClient from './client'
import type { Barber, BarberAvailability, Review } from '@/types/api.types'

export const barbersApi = {
  getAll() {
    return apiClient.get<Barber[]>('/barbers')
  },

  getById(id: string) {
    return apiClient.get<Barber>(`/barbers/${id}`)
  },

  getSlots(id: string, date: string, serviceIds: string[]) {
    const params = new URLSearchParams({ date })
    serviceIds.forEach((sid) => params.append('serviceIds', sid))
    return apiClient.get<string[]>(`/barbers/${id}/slots?${params.toString()}`)
  },

  getReviews(id: string) {
    return apiClient.get<Review[]>(`/barbers/${id}/reviews`)
  },

  updateAvailability(availability: BarberAvailability[]) {
    return apiClient.put<Barber>('/barbers/me/availability', availability)
  },
}
