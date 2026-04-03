import { http, HttpResponse } from 'msw'
import type {
  LoginResult,
  Barber,
  Service,
  Appointment,
  Review,
} from '@/types/api.types'

const BASE_URL = 'http://localhost:5000/api/v1'

// ─── Mock data fixtures ──────────────────────────────────────────────────────

export const mockLoginResult: LoginResult = {
  accessToken: 'mock-access-token',
  refreshToken: 'mock-refresh-token',
  role: 'Client',
  userId: 'user-1',
  barberId: null,
}

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
      { dayOfWeek: 1, startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 2, startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 3, startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 4, startTime: '09:00:00', endTime: '18:00:00' },
      { dayOfWeek: 5, startTime: '09:00:00', endTime: '18:00:00' },
    ],
  },
  {
    id: 'barber-2',
    userId: 'user-barber-2',
    name: 'Rafael Lima',
    email: 'rafael@imperador.com',
    averageRating: 4.5,
    availability: [
      { dayOfWeek: 2, startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 3, startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 4, startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 5, startTime: '10:00:00', endTime: '19:00:00' },
      { dayOfWeek: 6, startTime: '10:00:00', endTime: '17:00:00' },
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
  },
  {
    id: 'service-2',
    name: 'Barba',
    description: 'Modelagem e hidratação de barba',
    durationMinutes: 20,
    price: 35.0,
    isActive: true,
  },
  {
    id: 'service-3',
    name: 'Corte + Barba',
    description: 'Combo completo de corte e barba',
    durationMinutes: 50,
    price: 70.0,
    isActive: true,
  },
  {
    id: 'service-4',
    name: 'Hidratação',
    description: 'Tratamento capilar completo',
    durationMinutes: 40,
    price: 55.0,
    isActive: false,
  },
]

export const mockAppointments: Appointment[] = [
  {
    id: 'appt-1',
    clientId: 'user-1',
    clientName: 'João Silva',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000 * 2).toISOString(), // 2 days from now
    totalDurationMinutes: 30,
    status: 'Accepted',
    notes: null,
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    ],
    hasReview: false,
  },
  {
    id: 'appt-2',
    clientId: 'user-1',
    clientName: 'João Silva',
    barberId: 'barber-2',
    barberName: 'Rafael Lima',
    scheduledAt: new Date(Date.now() - 86400000 * 3).toISOString(), // 3 days ago
    totalDurationMinutes: 50,
    status: 'Completed',
    notes: 'Deixar lateral bem curta',
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-3', name: 'Corte + Barba', durationMinutes: 50, price: 70.0 },
    ],
    hasReview: false,
  },
]

export const mockBarberAppointments: Appointment[] = [
  {
    id: 'appt-pending-1',
    clientId: 'user-2',
    clientName: 'Pedro Costa',
    barberId: 'barber-1',
    barberName: 'Carlos Andrade',
    scheduledAt: new Date(Date.now() + 86400000).toISOString(),
    totalDurationMinutes: 30,
    status: 'Pending',
    notes: null,
    createdAt: new Date().toISOString(),
    services: [
      { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
    ],
    hasReview: false,
  },
  {
    id: 'appt-accepted-1',
    clientId: 'user-3',
    clientName: 'Maria Santos',
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
    hasReview: false,
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
  http.post(`${BASE_URL}/auth/login`, async ({ request }) => {
    const body = await request.json() as { email: string; password: string }
    if (body.email === 'barber@test.com') {
      return HttpResponse.json(mockBarberLoginResult)
    }
    return HttpResponse.json(mockLoginResult)
  }),

  http.post(`${BASE_URL}/auth/register/client`, async () => {
    return HttpResponse.json(mockLoginResult, { status: 201 })
  }),

  http.post(`${BASE_URL}/auth/register/barber`, async () => {
    return HttpResponse.json(mockBarberLoginResult, { status: 201 })
  }),

  http.post(`${BASE_URL}/auth/refresh`, async () => {
    return HttpResponse.json(mockLoginResult)
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
    const newAppointment: Appointment = {
      id: `appt-new-${Date.now()}`,
      clientId: 'user-1',
      clientName: 'João Silva',
      barberId: 'barber-1',
      barberName: 'Carlos Andrade',
      scheduledAt: new Date(Date.now() + 86400000).toISOString(),
      totalDurationMinutes: 30,
      status: 'Pending',
      notes: null,
      createdAt: new Date().toISOString(),
      services: [
        { id: 'service-1', name: 'Corte Clássico', durationMinutes: 30, price: 45.0 },
      ],
      hasReview: false,
    }
    return HttpResponse.json(newAppointment, { status: 201 })
  }),

  http.get(`${BASE_URL}/appointments/mine`, () => {
    return HttpResponse.json(mockAppointments)
  }),

  http.delete(`${BASE_URL}/appointments/:id`, () => {
    return new HttpResponse(null, { status: 204 })
  }),

  http.get(`${BASE_URL}/appointments/barber`, () => {
    return HttpResponse.json(mockBarberAppointments)
  }),

  http.patch(`${BASE_URL}/appointments/:id/accept`, ({ params }) => {
    const appt = mockBarberAppointments.find((a) => a.id === params.id)
    if (!appt) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({ ...appt, status: 'Accepted' })
  }),

  http.patch(`${BASE_URL}/appointments/:id/reject`, ({ params }) => {
    const appt = mockBarberAppointments.find((a) => a.id === params.id)
    if (!appt) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({ ...appt, status: 'Rejected' })
  }),

  http.patch(`${BASE_URL}/appointments/:id/complete`, ({ params }) => {
    const appt = mockBarberAppointments.find((a) => a.id === params.id)
    if (!appt) return new HttpResponse(null, { status: 404 })
    return HttpResponse.json({ ...appt, status: 'Completed' })
  }),

  // Reviews
  http.post(`${BASE_URL}/reviews`, async () => {
    return HttpResponse.json(
      {
        id: `review-new-${Date.now()}`,
        clientName: 'João Silva',
        rating: 5,
        comment: 'Ótimo serviço!',
        createdAt: new Date().toISOString(),
      },
      { status: 201 }
    )
  }),
]
