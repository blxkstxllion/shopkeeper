import { useEffect, useRef, useState } from 'react'
import { ChevronDown, FileText, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { exportBusinessReport, type ReportExportFormat } from '@/api/reports'
import { triggerDownload } from '@/lib/download'
import type { DateRange } from '@/components/ui/DateRangePicker'

/** Bundles Profitability + Expenses + Inventory for the currently selected range/branch into
 * one downloadable document, with a written summary on top - separate from the per-tab
 * "Export CSV" buttons, which stay raw-data-only. */
export function GenerateReportButton({ range, branchId }: { range: DateRange; branchId?: string }) {
  const [isOpen, setIsOpen] = useState(false)
  const [pending, setPending] = useState<ReportExportFormat | null>(null)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setIsOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  async function handleGenerate(format: ReportExportFormat) {
    setIsOpen(false)
    setPending(format)
    try {
      const { blob, filename } = await exportBusinessReport({ from: range.from, to: range.to, branchId, format })
      triggerDownload(filename, blob)
    } finally {
      setPending(null)
    }
  }

  return (
    <div className="relative" ref={ref}>
      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={() => setIsOpen((o) => !o)}
        disabled={pending !== null}
        aria-haspopup="true"
        aria-expanded={isOpen}
      >
        {pending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileText className="h-3.5 w-3.5" />}
        Generate report
        <ChevronDown className="h-3.5 w-3.5" />
      </Button>

      {isOpen && (
        <div className="absolute right-0 top-full z-10 mt-1 w-40 rounded-lg border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-700 dark:bg-slate-900">
          <button
            type="button"
            onClick={() => handleGenerate('Pdf')}
            className="block w-full px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            PDF
          </button>
          <button
            type="button"
            onClick={() => handleGenerate('Word')}
            className="block w-full px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            Word
          </button>
        </div>
      )}
    </div>
  )
}
