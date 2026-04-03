import apiClient from './client'
import type { Review, CreateReviewPayload } from '@/types/api.types'

export const reviewsApi = {
  create(payload: CreateReviewPayload) {
    return apiClient.post<Review>('/reviews', payload)
  },
}
