import { describe, it, expect } from 'vitest'
import { normalizeBrPhone, isValidBrPhone } from '@/lib/utils/phone'

describe('normalizeBrPhone', () => {
  it('strips formatting and prefixes +55', () => {
    expect(normalizeBrPhone('(11) 99999-0000')).toBe('+5511999990000')
  })

  it('is idempotent when already normalized', () => {
    expect(normalizeBrPhone('+5511999990000')).toBe('+5511999990000')
  })
})

describe('isValidBrPhone', () => {
  it('accepts a normalized 11-digit mobile number', () => {
    expect(isValidBrPhone('+5511999990000')).toBe(true)
  })

  it('rejects a number that is too short', () => {
    expect(isValidBrPhone('+551199990000')).toBe(false)
  })

  it('rejects an empty string', () => {
    expect(isValidBrPhone('')).toBe(false)
  })
})
