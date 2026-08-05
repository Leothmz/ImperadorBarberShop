import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '../../test-utils'
import { BookingConfirmation } from '@/components/booking/BookingConfirmation'
import type { Barber, Service } from '@/types/api.types'

const barber = {
  id: 'barber-1',
  userId: 'user-1',
  name: 'Carlos',
  email: 'carlos@imperador.com',
  photoUrl: null,
  averageRating: 4.5,
  isActive: true,
  availability: [],
} as unknown as Barber

const services = [
  {
    id: 'service-1',
    name: 'Corte',
    description: 'Corte clássico',
    durationMinutes: 30,
    price: 35,
    isActive: true,
    photoUrl: null,
    addons: [],
  },
] as unknown as Service[]

function renderConfirmation(clientPhone: string) {
  return render(
    <BookingConfirmation
      barber={barber}
      services={services}
      selectedDate={new Date('2026-08-10T00:00:00')}
      selectedSlot="09:00:00"
      notes=""
      onNotesChange={vi.fn()}
      clientName="Cliente Teste"
      onClientNameChange={vi.fn()}
      clientPhone={clientPhone}
      onClientPhoneChange={vi.fn()}
      onConfirm={vi.fn()}
      isLoading={false}
    />
  )
}

const confirmButton = () => screen.getByRole('button', { name: /confirmar agendamento/i })

describe('BookingConfirmation', () => {
  // O telefone é normalizado antes de ir para a API, então o botão precisa
  // aceitar os mesmos formatos que a normalização aceita.
  it.each([
    ['11999998888', 'só dígitos, sem DDI'],
    ['+5511999998888', 'formato canônico'],
    ['(11) 99999-8888', 'com máscara'],
    ['+55 11 99999-8888', 'igual ao placeholder'],
  ])('habilita o botão com %s (%s)', (phone) => {
    renderConfirmation(phone)
    expect(confirmButton()).toBeEnabled()
  })

  it.each([
    ['', 'vazio'],
    ['11999', 'curto demais'],
  ])('mantém o botão desabilitado com %s (%s)', (phone) => {
    renderConfirmation(phone)
    expect(confirmButton()).toBeDisabled()
  })

  // A API fala horário de parede ("09:00:00"), sem fuso. Tratar o slot como UTC
  // fazia o resumo mostrar 06:00 para um agendamento das 09:00 (UTC-3).
  it('mostra no resumo o mesmo horário que foi escolhido', () => {
    renderConfirmation('11999998888')
    expect(screen.getByText(/10\/08\/2026 às 09:00/)).toBeInTheDocument()
  })
})
