import { Plus, X } from 'lucide-react'
import { getComparisonPresets } from '@/lib/reportComparison'
import { DateRangePicker, type DateRange } from '@/components/ui/DateRangePicker'
import type { useReportComparison } from './useReportComparison'

type ComparisonState = ReturnType<typeof useReportComparison>

/** The "+ Compare" pill / preset-picker / active-comparison-label UI, shared across the
 * Profitability, Expenses, and Inventory report tabs - pair with useReportComparison for state. */
export function ReportCompareControl({
  range,
  compareRange,
  compareLabel,
  showCompareOptions,
  showCustomComparePicker,
  setShowCompareOptions,
  toggleCustomPicker,
  selectCompareRange,
  clearCompare,
}: Pick<
  ComparisonState,
  | 'compareRange'
  | 'compareLabel'
  | 'showCompareOptions'
  | 'showCustomComparePicker'
  | 'setShowCompareOptions'
  | 'toggleCustomPicker'
  | 'selectCompareRange'
  | 'clearCompare'
> & { range: DateRange }) {
  if (compareRange) {
    return (
      <div className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
        <span>
          Comparing to <span className="font-medium text-slate-700 dark:text-slate-300">{compareLabel}</span> (
          {compareRange.from} – {compareRange.to})
        </span>
        <button
          type="button"
          onClick={clearCompare}
          className="flex items-center gap-0.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
        >
          <X className="h-3.5 w-3.5" />
          Clear
        </button>
      </div>
    )
  }

  if (showCompareOptions) {
    return (
      <div className="flex flex-wrap items-center gap-2">
        {getComparisonPresets(range).map((p) => (
          <button
            key={p.label}
            type="button"
            onClick={() => selectCompareRange(p.range, p.label)}
            className="rounded-full border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:border-primary-400 hover:text-primary-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-primary-500 dark:hover:text-primary-400"
          >
            {p.label}
          </button>
        ))}
        <button
          type="button"
          onClick={toggleCustomPicker}
          className="rounded-full border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:border-primary-400 hover:text-primary-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-primary-500 dark:hover:text-primary-400"
        >
          Custom range
        </button>
        {showCustomComparePicker && (
          <DateRangePicker value={compareRange ?? range} onChange={(r) => selectCompareRange(r, 'Custom range')} />
        )}
        <button
          type="button"
          onClick={() => setShowCompareOptions(false)}
          className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
        >
          Cancel
        </button>
      </div>
    )
  }

  return (
    <button
      type="button"
      onClick={() => setShowCompareOptions(true)}
      className="flex items-center gap-1 rounded-full border border-dashed border-slate-300 px-3 py-1 text-xs font-medium text-slate-500 hover:border-primary-400 hover:text-primary-700 dark:border-slate-600 dark:text-slate-400 dark:hover:border-primary-500 dark:hover:text-primary-400"
    >
      <Plus className="h-3.5 w-3.5" />
      Compare
    </button>
  )
}
