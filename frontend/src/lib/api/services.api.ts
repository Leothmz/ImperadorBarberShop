import apiClient from './client'
import type { Service } from '@/types/api.types'

export const servicesApi = {
  getAll() {
    return apiClient.get<Service[]>('/services')
  },
}
