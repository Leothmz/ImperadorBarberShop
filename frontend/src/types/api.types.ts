export type UserRole = 'Barber'
export type AppointmentStatus = 'Accepted' | 'Cancelled' | 'Completed'

export interface Service {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  isActive: boolean
}

export type DayOfWeekString =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export interface BarberAvailability {
  dayOfWeek: DayOfWeekString // API returns string enum (JsonStringEnumConverter)
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
  clientName: string
  clientPhone: string
  barberId: string
  barberName: string
  scheduledAt: string // ISO datetime
  totalDurationMinutes: number
  status: AppointmentStatus
  notes: string | null
  createdAt: string
  services: ServiceRef[]
}

export interface AppointmentManage {
  id: string
  clientName: string
  barberName: string
  scheduledAt: string
  totalDurationMinutes: number
  status: AppointmentStatus
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

export interface RegisterBarberPayload {
  name: string
  email: string
  password: string
  availability: BarberAvailability[]
}

export interface CreateAppointmentPayload {
  clientName: string
  clientPhone: string
  barberId: string
  scheduledAt: string
  serviceIds: string[]
  notes?: string
}

export interface CreateAppointmentResult {
  id: string
  accessToken: string
}

export interface CreateReviewByTokenPayload {
  rating: number
  comment?: string
}
