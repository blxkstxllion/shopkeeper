import type { DateRange } from '@/components/ui/DateRangePicker'

function parseIsoDate(s: string): Date {
  const [year, month, day] = s.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function addDays(d: Date, n: number): Date {
  const result = new Date(d)
  result.setDate(result.getDate() + n)
  return result
}

function addYears(d: Date, n: number): Date {
  const result = new Date(d)
  result.setFullYear(result.getFullYear() + n)
  return result
}

/** Immediately-preceding range of the same length as the primary range, e.g. primary
 * "Aug 1-31" (31 days) -> comparison "Jul 1-31". */
function previousPeriod(primary: DateRange): DateRange {
  const from = parseIsoDate(primary.from)
  const to = parseIsoDate(primary.to)
  const lengthDays = Math.round((to.getTime() - from.getTime()) / 86_400_000) + 1
  return { from: toIsoDate(addDays(from, -lengthDays)), to: toIsoDate(addDays(from, -1)) }
}

/** Same month/day span, one calendar year earlier. */
function samePeriodLastYear(primary: DateRange): DateRange {
  return {
    from: toIsoDate(addYears(parseIsoDate(primary.from), -1)),
    to: toIsoDate(addYears(parseIsoDate(primary.to), -1)),
  }
}

export function getComparisonPresets(primary: DateRange): { label: string; range: DateRange }[] {
  return [
    { label: 'Previous period', range: previousPeriod(primary) },
    { label: 'Same period last year', range: samePeriodLastYear(primary) },
  ]
}

/** Percent change from `previous` to `current`, rounded to 1 decimal.
 * `null` when there's nothing to compare (both zero); `'new'` when the metric went
 * from zero to something (a plain percentage would be a divide-by-zero). */
export function computePercentChange(current: number, previous: number): number | 'new' | null {
  if (previous === 0 && current === 0) return null
  if (previous === 0) return 'new'
  return Math.round(((current - previous) / Math.abs(previous)) * 1000) / 10
}
