import { useState } from 'react'
import { computePercentChange } from '@/lib/reportComparison'
import type { DateRange } from '@/components/ui/DateRangePicker'
import type { StatDelta } from '@/components/ui/StatTile'

/** Shared state + delta-computation for a report tab's "Compare to..." control - see
 * ReportCompareControl.tsx for the matching UI. Split from the tabs themselves since all
 * three (Profitability/Expenses/Inventory) need the identical picker/clear/label behavior. */
export function useReportComparison() {
  const [compareRange, setCompareRange] = useState<DateRange | null>(null)
  const [compareLabel, setCompareLabel] = useState('comparison range')
  const [showCompareOptions, setShowCompareOptions] = useState(false)
  const [showCustomComparePicker, setShowCustomComparePicker] = useState(false)

  const vsLabel = `vs ${compareLabel.toLowerCase()}`

  function delta(current: number, previous: number | undefined, goodDirection?: 'up' | 'down'): StatDelta | undefined {
    if (previous === undefined) return undefined
    return { percent: computePercentChange(current, previous), label: vsLabel, goodDirection }
  }

  function selectCompareRange(selected: DateRange, label: string) {
    setCompareRange(selected)
    setCompareLabel(label)
    setShowCompareOptions(false)
    setShowCustomComparePicker(false)
  }

  function clearCompare() {
    setCompareRange(null)
    setShowCompareOptions(false)
    setShowCustomComparePicker(false)
  }

  function toggleCustomPicker() {
    setShowCustomComparePicker((s) => !s)
  }

  return {
    compareRange,
    compareLabel,
    showCompareOptions,
    showCustomComparePicker,
    setShowCompareOptions,
    toggleCustomPicker,
    selectCompareRange,
    clearCompare,
    delta,
  }
}
