import { describe, it, expect } from 'vitest'
import { formatBrPhone, isValidBrPhone, normalizeBrPhone } from '@/lib/utils/phone'

describe('normalizeBrPhone', () => {
  it('strips formatting and prefixes +55', () => {
    expect(normalizeBrPhone('(11) 99999-0000')).toBe('+5511999990000')
  })

  it('is idempotent when already normalized', () => {
    expect(normalizeBrPhone('+5511999990000')).toBe('+5511999990000')
  })

  // Os mesmos casos do BrazilianPhoneTests do backend: tela e API aceitam o mesmo
  it.each([
    ['5511999990000', 'com 55, sem +'],
    ['11 9 9999-0000', 'com espaços'],
    ['11 9999-0000', 'sem o nono dígito'],
    ['+55 11 9999-0000', 'com 55, sem o nono dígito'],
    ['551199990000', '55 grudado, sem o nono dígito'],
    ['011 99999-0000', 'com zero de discagem'],
    ['+55 (011) 99999-0000', 'com 55 e zero de discagem'],
    ['0055 11 99999-0000', 'com prefixo internacional'],
  ])('reduces %s (%s) to the canonical form', (input) => {
    expect(normalizeBrPhone(input)).toBe('+5511999990000')
  })

  it('does not mistake area code 55 for the country code', () => {
    expect(normalizeBrPhone('55 99999-0000')).toBe('+5555999990000')
    expect(normalizeBrPhone('+55 55 99999-0000')).toBe('+5555999990000')
  })

  it.each([
    ['', 'vazio'],
    ['11999', 'curto demais'],
    ['99999-0000', 'sem DDD'],
    ['+1 555 123 4567', 'estrangeiro'],
    ['10 99999-0000', 'DDD inexistente'],
    ['11 3333-4444', 'fixo'],
  ])('rejects %s (%s)', (input) => {
    expect(normalizeBrPhone(input)).toBeNull()
  })
})

describe('isValidBrPhone', () => {
  it('accepts a number typed without the ninth digit', () => {
    expect(isValidBrPhone('11 9999-0000')).toBe(true)
  })

  it('rejects a number that is too short', () => {
    expect(isValidBrPhone('+5511999')).toBe(false)
  })

  it('rejects an empty string', () => {
    expect(isValidBrPhone('')).toBe(false)
  })
})

describe('formatBrPhone', () => {
  it('formats a canonical phone for display', () => {
    expect(formatBrPhone('+5511999990000')).toBe('(11) 99999-0000')
  })

  it('leaves anything else untouched', () => {
    expect(formatBrPhone('não informado')).toBe('não informado')
  })
})
