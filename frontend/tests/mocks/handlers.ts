import { http, HttpResponse } from 'msw'
import type {
  LoginResult,
  Barber,
  Service,
  Appointment,
  AppointmentManage,
  Review,
} from '@/types/api.types'

const BASE_URL = 'http://localhost:5000/api/v1'

// ─── Mock data fixtures ──────────────────────────────────────────────────────

export const mockBarberLoginResult: LoginResult = {
  accessToken: 'mock-barber-access-token',
  refreshToken: 'mock-barber-refresh-token',
  role: 'Barber',
  userId: 'user-barber-1',
  barberId: 'barber-1',
}

export const mockBarbers: Barber[] = [
  {
    id: 'barber-1',
    userId: 'user-barber-1',
    name: 'Carlos Andrade',
    email: 'carlos@imperador.com',
    averageRating: 4.8,
    availability: [
      { dayOfWeek: 'Monday', startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 'Tuesday', startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 'Wednesday', startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 'Thursday', startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 'Friday', startTime: '09:00:00', endTime: '18:00:00' },
    ],
  },
  {
    id: 'barber-2',
    userId: 'user-barber-2',
    name: 'Rafael Lima',
    email: 'rafael@imperador.com',
    averageRating: 4.5,
    availability: [
      { dayOfWeek: 'Tuesday', startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 'Wednesday', startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 'Thursday', startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 'Friday', startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 'Saturday', startTime: '10:00:00', endTime: '17:00:00' },
    ],
  },
]

export const mockServices: Service[] = [
  {
    id: 'service-1',
    name: 'Corte Clássico',
    description: 'Corte tradicional com acabamento perfeito',
    durationMinutes: 30,
    price: 45.0,
    isActive: true,
    photoUrl: null,
    addons: [
      { id: 'addon-barba', name: 'Barba', description: '', durationMinutes: 20, price: 25.0, photoUrl: null },
    ],
  },
  {
    id: 'service-2',
    name: 'Barba',
    description: 'Modelagem e hidratação de barba',
    durationMinutes: 20,
    price: 35.0,
    isActive: true,
    photoUrl: null,
    addons: [],
  },
  {
    id: 'service-3',
    name: 'Corte + Barba',
    description: 'Combo completo de corte e barba',
    durationMinutes: 50,
    price: 70.0,
    isActive: true,
    photoUrl: null,
    addons: [],
  },
  {
    id: 'service-4',
    name: 'Hidratação',
    description: 'Tratamento capilar completo',
    durationMinutes: 40,
    price: 55.0,
    isActive: false,
    photoUrl: null,
    addons: [],
  },
]

export const mockManagedAppointment: AppointmentManage = {
  id: 'appt-1',
  clientName: 'João Silva',
  barberName: 'Carlos Andrade',
  scheduledAt: new Date(Date.now() + 86400000 * 2).toISOString(),
  totalDurationMinutes: 30,
  status: 'Accepted',
  services: [
    { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
  ],
}

export const mockBarberAppointments: Appointment[] = [
  {
    id: 'appt-accepted-1',
    clientName: 'Pedro Costa',
    clientPhone: '+5511999990000',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000).toISOString(),
    totalDurationMinutes: 30,
    status: 'Accepted',
    notes: null,
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    ],
  },
  {
    id: 'appt-accepted-2',
    clientName: 'Maria Santos',
    clientPhone: '+5511999990001',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000 * 3).toISOString(),
    totalDurationMinutes: 50,
    status: 'Accepted',
    notes: 'Primeira vez aqui',
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-3', name: 'Corte + Barba', durationMinutes: 50, price: 70.0 },
    ],
  },
]

export const mockSlots: string[] = [
  '09:00:00',
  '09:30:00',
  '10:00:00',
  '10:30:00',
  '11:00:00',
  '14:00:00',
  '14:30:00',
  '15:00:00',
]

export const mockReviews: Review[] = [
  {
    id: 'review-1',
    clientName: 'João Silva',
    rating: 5,
    comment: 'Excelente serviço! Voltarei com certeza.',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'review-2',
    clientName: 'Ana Paula',
    rating: 4,
    comment: null,
    createdAt: new Date().toISOString(),
  },
]

// ─── Handlers ────────────────────────────────────────────────────────────────

export const handlers = [
  // Auth
  http.post(`${BASE_URL}/auth/login`, async () => {
    return HttpResponse.json(mockBarberLoginResult)
  }),

  http.post(`${BASE_URL}/auth/register/barber`, async () => {
    return HttpResponse.json(mockBarberLoginResult, { status: 201 })
  }),

  http.post(`${BASE_URL}/auth/refresh`, async () => {
    return HttpResponse.json(mockBarberLoginResult)
  }),

  // Services
  http.get(`${BASE_URL}/services`, () => {
    return HttpResponse.json(mockServices)
  }),

  // Barbers
  http.get(`${BASE_URL}/barbers`, () => {
    return HttpResponse.json(mockBarbers)
  }),

  http.get(`${BASE_URL}/barbers/:id`, ({ params }) => {
    const barber = mockBarbers.find((b) => b.id === params.id)
    if (!barber) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json(barber)
  }),

  http.get(`${BASE_URL}/barbers/:id/slots`, () => {
    return HttpResponse.json(mockSlots)
  }),

  http.get(`${BASE_URL}/barbers/:id/reviews`, () => {
    return HttpResponse.json(mockReviews)
  }),

  http.put(`${BASE_URL}/barbers/me/availability`, async ({ request }) => {
    const body = await request.json()
    return HttpResponse.json({ ...mockBarbers[0], availability: body })
  }),

  // Appointments
  http.post(`${BASE_URL}/appointments`, async () => {
    return HttpResponse.json({ id: 'appt-new-1', accessToken: 'mock-access-token-1' }, { status: 201 })
  }),

  http.get(`${BASE_URL}/appointments/manage/:token`, () => {
    return HttpResponse.json(mockManagedAppointment)
  }),

  http.post(`${BASE_URL}/appointments/manage/:token/cancel`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.post(`${BASE_URL}/appointments/manage/:token/review`, async () => {
    return HttpResponse.json({ id: 'review-new-1' }, { status: 201 })
  }),

  http.get(`${BASE_URL}/appointments/barber`, () => {
    return HttpResponse.json(mockBarberAppointments)
  }),

  http.patch(`${BASE_URL}/appointments/:id/cancel-by-barber`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.patch(`${BASE_URL}/appointments/:id/complete`, ({ params }) => {
    const appt = mockBarberAppointments.find((a) => a.id === params.id)
    if (!appt) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({ ...appt, status: 'Completed' })
  }),
]
