export type UserRole = 'Client' | 'Barber'
export type AppointmentStatus = 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Completed'

export interface Service {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  isActive: boolean
}

export interface BarberAvailability {
  dayOfWeek: number // 0=Sun, 1=Mon, ...
  startTime: string // "HH:mm:ss"
  endTime: string
}

export interface Barber {
  id: string
  userId: string
  name: string
  email: string
  averageRating: number
  availability: BarberAvailability[]
}

export interface ServiceRef {
  id: string
  name: string
  durationMinutes: number
  price: number
}

export interface Appointment {
  id: string
  clientId: string
  clientName: string
  barberId: string
  barberName: string
  scheduledAt: string // ISO datetime
  totalDurationMinutes: number
  status: AppointmentStatus
  notes: string | null
  createdAt: string
  services: ServiceRef[]
}

export interface Review {
  id: string
  clientName: string
  rating: number
  comment: string | null
  createdAt: string
}

export interface LoginResult {
  accessToken: string
  refreshToken: string
  role: UserRole
  userId: string
  barberId: string | null
}

// Request payload types
export interface LoginPayload {
  email: string
  password: string
}

export interface RegisterClientPayload {
  name: string
  email: string
  password: string
}

export interface RegisterBarberPayload {
  name: string
  email: string
  password: string
  availability: BarberAvailability[]
}

export interface CreateAppointmentPayload {
  barberId: string
  scheduledAt: string
  serviceIds: string[]
  notes?: string
}

export interface CreateReviewPayload {
  appointmentId: string
  rating: number
  comment?: string
}
