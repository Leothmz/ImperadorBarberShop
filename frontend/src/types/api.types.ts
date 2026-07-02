export type UserRole = 'Barber' | 'Admin'
export type AppointmentStatus = 'Accepted' | 'Cancelled' | 'Completed'

export interface Service {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  isActive: boolean
  photoUrl: string | null
  addons: ServiceAddon[]
}

export interface ServiceAddon {
  id: string
  name: string
  description: string
  durationMinutes: number
  price: number
  photoUrl: string | null
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
  photoUrl: string | null
  isActive: boolean
  availability: BarberAvailability[]
}

// Admin barber (includes isActive)
export interface AdminBarber extends Barber {
  email: string
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

// Financial types
export interface FinancialSummary {
  totalRevenue: number
  totalAppointments: number
  averageTicket: number
  from: string
  to: string
}

export interface FinancialByBarberItem {
  barberId: string
  barberName: string
  appointments: number
  revenue: number
}

export interface FinancialByServiceItem {
  serviceId: string
  serviceName: string
  count: number
  revenue: number
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

export interface CreateReviewByTokenResult {
  id: string
}

// Admin request types
export interface CreateBarberPayload {
  name: string
  email: string
  password: string
  availability: BarberAvailability[]
  photo?: File
}

export interface CreateServicePayload {
  name: string
  description: string
  price: number
  durationMinutes: number
  photo?: File
}

export interface UpdateServicePayload extends CreateServicePayload {
  id: string
}

// WhatsApp / Notifications
export type WhatsAppConnectionStatus = 'connected' | 'disconnected' | 'qr_required'

export interface WhatsAppStatus {
  status: WhatsAppConnectionStatus
  phoneNumber?: string | null
}

export interface WhatsAppQr {
  qrCode: string
}

export interface NotificationSettings {
  channels: string[]
  reminderMinutesBefore: number
  notificationPhone?: string | null
}

export interface UpdateNotificationSettingsPayload {
  channels: string[]
  reminderMinutesBefore: number
  notificationPhone?: string | null
}
