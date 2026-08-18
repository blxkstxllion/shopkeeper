import { describe, expect, it } from 'vitest'
import { computePercentChange, getComparisonPresets } from './reportComparison'

describe('getComparisonPresets', () => {
  it('computes the immediately-preceding period of the same length', () => {
    const [previousPeriod] = getComparisonPresets({ from: '2026-09-01', to: '2026-09-30' })
    expect(previousPeriod.label).toBe('Previous period')
    expect(previousPeriod.range).toEqual({ from: '2026-08-02', to: '2026-08-31' })
  })

  it('handles a single-day primary range', () => {
    const [previousPeriod] = getComparisonPresets({ from: '2026-08-15', to: '2026-08-15' })
    expect(previousPeriod.range).toEqual({ from: '2026-08-14', to: '2026-08-14' })
  })

  it('computes the same month/day span one calendar year earlier', () => {
    const [, sameLastYear] = getComparisonPresets({ from: '2026-08-01', to: '2026-08-31' })
    expect(sameLastYear.label).toBe('Same period last year')
    expect(sameLastYear.range).toEqual({ from: '2025-08-01', to: '2025-08-31' })
  })
})

describe('computePercentChange', () => {
  it('returns a positive percent for an increase', () => {
    expect(computePercentChange(150, 100)).toBe(50)
  })

  it('returns a negative percent for a decrease', () => {
    expect(computePercentChange(50, 100)).toBe(-50)
  })

  it('returns null when both values are zero', () => {
    expect(computePercentChange(0, 0)).toBeNull()
  })

  it('returns "new" when going from zero to a nonzero value', () => {
    expect(computePercentChange(50, 0)).toBe('new')
  })

  it('uses the absolute value of a negative previous total as the denominator', () => {
    // Net profit improving from -100 to -50 is a 50% improvement, not a negative percentage.
    expect(computePercentChange(-50, -100)).toBe(50)
  })

  it('rounds to one decimal place', () => {
    expect(computePercentChange(100, 33)).toBe(203)
  })
})
