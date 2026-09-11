export type UserRole = 'Barber' | 'Admin'
export type AppointmentStatus = 'Accepted' | 'Cancelled' | 'Completed'
export type PaymentMethod = 'Dinheiro' | 'Cartão' | 'Pix' | 'Plano'
/** Plano: o cliente paga o plano nesta visita, ou a visita é coberta por um plano já pago. */
export type PlanKind = 'Pagamento' | 'Recorrencia'
/** Como um pagamento de plano foi feito. Um "Cartão" só, sem crédito/débito. */
export type PlanTender = Exclude<PaymentMethod, 'Plano'>

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
  paymentMethod: PaymentMethod | null
  paidAt: string | null // nulo também na recorrência de plano: nada foi pago na visita
  planKind: PlanKind | null
  planTender: PlanTender | null
  chargedAmount: number | null
  /** Valor do atendimento no financeiro: o cobrado no plano, senão a soma dos preços do agendamento. */
  effectiveAmount: number
}

/** O pagamento enviado ao concluir ou ao registrar depois. Os campos do plano só existem com Plano. */
export type AppointmentPayment =
  | { paymentMethod: PlanTender }
  | { paymentMethod: 'Plano'; planKind: 'Pagamento'; planTender: PlanTender; chargedAmount: number }
  | { paymentMethod: 'Plano'; planKind: 'Recorrencia' }

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
  totalExpenses: number
  netRevenue: number
}

export interface Expense {
  id: string
  amount: number
  description: string
  date: string        // "YYYY-MM-DD"
  createdAt: string
}

export interface FinancialTimelineItem {
  period: string      // "YYYY-MM-DD" (start of day/week/month)
  revenue: number
  appointments: number
}

export interface CreateExpensePayload {
  amount: number
  description: string
  date: string        // "YYYY-MM-DD"
}

export interface FinancialByBarberItem {
  barberId: string
  barberName: string
  appointments: number
  revenue: number
}

/** Cliente quase perdido: última visita concluída entre 25 e 30 dias atrás. */
export interface ReinviteCandidate {
  clientId: string
  name: string
  phone: string // canônico, "+5511999990000"
  lastVisitAt: string // horário de parede, sem fuso
  daysSinceLastVisit: number
  visitCount: number
}

export interface FinancialByServiceItem {
  serviceId: string
  serviceName: string
  count: number
  revenue: number
}

// The refresh token is not here: the API sets it as an HttpOnly cookie.
export interface LoginResult {
  accessToken: string
  role: UserRole
  userId: string
  barberId: string | null
}

// Request payload types
export interface LoginPayload {
  email: string
  password: string
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

export interface UpdateBarberPayload {
  id: string
  name: string
  email: string
  password?: string
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
