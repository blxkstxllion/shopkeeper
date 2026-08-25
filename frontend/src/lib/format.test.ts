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
    // Not "US$80.00" - that was en-GH's disambiguated rendering from before formatMoney mapped
    // each currency to its own locale. USD now uses en-US, which renders the plain "$" a US
    // business actually expects.
    expect(formatMoney(80)).toBe('$80.00')
  })

  it('formats GBP under its own locale, not GHS conventions', () => {
    setActiveCurrencyCode('GBP')
    expect(formatMoney(80)).toBe('£80.00')
  })

  it('falls back to a plain "CODE amount" format for a currency code Intl does not recognize, instead of throwing', () => {
    setActiveCurrencyCode('NOTREAL')
    expect(formatMoney(80)).toBe('NOTREAL 80.00')
  })
})
