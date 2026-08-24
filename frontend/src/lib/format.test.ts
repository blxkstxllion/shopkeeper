import { afterEach, describe, expect, it } from 'vitest'
import { formatMoney, setActiveCurrencyCode } from './format'

describe('formatMoney', () => {
  afterEach(() => {
    setActiveCurrencyCode('GHS') // restore the default so tests don't leak state into each other
  })

  it('defaults to GHS with the correct symbol', () => {
    expect(formatMoney(80)).toBe('GH₵80.00')
  })

  it('switches currency after setActiveCurrencyCode', () => {
    setActiveCurrencyCode('USD')
    expect(formatMoney(80)).toBe('US$80.00')
  })

  it('falls back to a plain "CODE amount" format for a currency code Intl does not recognize, instead of throwing', () => {
    setActiveCurrencyCode('NOTREAL')
    expect(formatMoney(80)).toBe('NOTREAL 80.00')
  })
})
