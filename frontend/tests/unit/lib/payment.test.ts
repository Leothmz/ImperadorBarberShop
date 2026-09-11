import { describe, it, expect } from 'vitest'
import { describePayment, describePlanAmount, parseChargedAmount } from '@/lib/utils/payment'

describe('parseChargedAmount', () => {
  it.each([
    ['120', 120],
    ['49,90', 49.9],
    ['49.90', 49.9],
    ['0', 0],
    ['0,00', 0],
    [' R$ 35,5 ', 35.5],
  ])('reads %j as %d', (raw, amount) => {
    expect(parseChargedAmount(raw)).toEqual({ amount, error: null })
  })

  it('treats an empty field as not filled yet, not as an error', () => {
    expect(parseChargedAmount('   ')).toEqual({ amount: null, error: null })
  })

  it('refuses a negative amount, like the API', () => {
    expect(parseChargedAmount('-10')).toEqual({ amount: null, error: 'O valor não pode ser negativo.' })
  })

  it('refuses more than two decimal places, like the API', () => {
    expect(parseChargedAmount('10,555')).toEqual({ amount: null, error: 'Use no máximo duas casas decimais.' })
  })

  it.each(['abc', '12,3,4', '1.234,56', ','])('refuses %j as not a value in reais', (raw) => {
    expect(parseChargedAmount(raw).amount).toBeNull()
    expect(parseChargedAmount(raw).error).toMatch(/Digite o valor em reais/)
  })
})

describe('describePayment', () => {
  it('is null without a registered payment', () => {
    expect(describePayment({ paymentMethod: null, planKind: null, planTender: null })).toBeNull()
  })

  it('is just the method outside the plan', () => {
    expect(describePayment({ paymentMethod: 'Cartão', planKind: null, planTender: null })).toBe('Cartão')
  })

  it('names the plan payment and its tender', () => {
    expect(describePayment({ paymentMethod: 'Plano', planKind: 'Pagamento', planTender: 'Pix' }))
      .toBe('Plano · Pagamento · Pix')
  })

  it('names the plan recurrence without a tender', () => {
    expect(describePayment({ paymentMethod: 'Plano', planKind: 'Recorrencia', planTender: null }))
      .toBe('Plano · Recorrência')
  })
})

describe('describePlanAmount', () => {
  it('is null outside the plan', () => {
    expect(describePlanAmount({ paymentMethod: 'Pix', planKind: null, chargedAmount: null })).toBeNull()
  })

  it('shows what the plan payment charged', () => {
    expect(describePlanAmount({ paymentMethod: 'Plano', planKind: 'Pagamento', chargedAmount: 120 }))
      .toMatch(/^R\$\s*120,00 cobrados no plano$/)
  })

  it('shows that the recurrence charged nothing at this visit', () => {
    expect(describePlanAmount({ paymentMethod: 'Plano', planKind: 'Recorrencia', chargedAmount: 0 }))
      .toMatch(/^Coberto pelo plano: R\$\s*0,00 nesta visita$/)
  })
})
