import { describe, expect, it } from 'vitest'
import { calculateSaleTotal } from './cart'

describe('calculateSaleTotal', () => {
  it('returns the taxable amount unchanged when tax is not enabled', () => {
    expect(calculateSaleTotal(150, { taxEnabled: false, taxRatePercent: 10, taxInclusivePricing: false })).toEqual({
      taxAmount: 0,
      total: 150,
    })
  })

  it('returns the taxable amount unchanged when settings have not loaded yet', () => {
    // The real-world fallback for a checkout opened before business settings have ever been
    // cached offline - must match the backend's own null-BusinessSetting behavior (no tax),
    // not silently block checkout.
    expect(calculateSaleTotal(150, undefined)).toEqual({ taxAmount: 0, total: 150 })
  })

  it('adds tax on top for tax-exclusive pricing', () => {
    // Regression test for the real bug: CheckoutModal used to compute total as just
    // subtotal - discounts, with zero tax awareness, while CreateSaleCommand strictly adds tax
    // on top for tax-exclusive businesses and rejects any payment total that doesn't match -
    // checkout was completely broken for this configuration.
    const result = calculateSaleTotal(140, { taxEnabled: true, taxRatePercent: 10, taxInclusivePricing: false })
    expect(result).toEqual({ taxAmount: 14, total: 154 })
  })

  it('backs tax out of an already-tax-inclusive taxable amount instead of adding it on top', () => {
    const result = calculateSaleTotal(110, { taxEnabled: true, taxRatePercent: 10, taxInclusivePricing: true })
    expect(result.total).toBe(110) // price already includes tax - total doesn't change
    expect(result.taxAmount).toBeCloseTo(10, 2) // but the tax portion is still broken out for display
  })

  it('rounds to the cent the same way the backend does', () => {
    const result = calculateSaleTotal(99.99, { taxEnabled: true, taxRatePercent: 7.5, taxInclusivePricing: false })
    expect(result.taxAmount).toBe(7.5)
    expect(result.total).toBe(107.49)
  })

  it('ignores a zero or negative tax rate', () => {
    expect(calculateSaleTotal(150, { taxEnabled: true, taxRatePercent: 0, taxInclusivePricing: false })).toEqual({
      taxAmount: 0,
      total: 150,
    })
  })
})
