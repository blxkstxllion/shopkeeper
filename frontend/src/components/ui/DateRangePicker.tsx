import { useEffect, useRef, useState } from 'react'
import { Calendar, Check, ChevronDown } from 'lucide-react'
import { Input } from './Input'

export interface DateRange {
  from: string
  to: string
}

function toIsoDate(d: Date): string {
  return d.toISOString().slice(0, 10)
}

function daysAgo(n: number): Date {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return d
}

function preset(from: Date, to: Date): DateRange {
  return { from: toIsoDate(from), to: toIsoDate(to) }
}

const PRESETS: { label: string; range: () => DateRange }[] = [
  { label: 'Today', range: () => preset(new Date(), new Date()) },
  { label: 'Yesterday', range: () => preset(daysAgo(1), daysAgo(1)) },
  { label: 'Last 7 days', range: () => preset(daysAgo(6), new Date()) },
  { label: 'Last 30 days', range: () => preset(daysAgo(29), new Date()) },
  { label: 'Last 90 days', range: () => preset(daysAgo(89), new Date()) },
  { label: 'This year', range: () => preset(new Date(new Date().getFullYear(), 0, 1), new Date()) },
]

function formatDisplay(range: DateRange): string {
  const matched = PRESETS.find((p) => {
    const r = p.range()
    return r.from === range.from && r.to === range.to
  })
  if (matched) return matched.label
  return `${range.from} – ${range.to}`
}

export function DateRangePicker({ value, onChange }: { value: DateRange; onChange: (range: DateRange) => void }) {
  const [isOpen, setIsOpen] = useState(false)
  const [customFrom, setCustomFrom] = useState(value.from)
  const [customTo, setCustomTo] = useState(value.to)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setIsOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  useEffect(() => {
    setCustomFrom(value.from)
    setCustomTo(value.to)
  }, [value])

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setIsOpen((o) => !o)}
        className="flex h-10 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
      >
        <Calendar className="h-4 w-4 text-slate-400" />
        {formatDisplay(value)}
        <ChevronDown className="h-3.5 w-3.5 text-slate-400" />
      </button>

      {isOpen && (
        <div className="absolute left-0 top-full z-10 mt-1 w-64 rounded-lg border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-700 dark:bg-slate-900">
          {PRESETS.map((p) => {
            const r = p.range()
            const isSelected = r.from === value.from && r.to === value.to
            return (
              <button
                key={p.label}
                type="button"
                onClick={() => {
                  onChange(r)
                  setIsOpen(false)
                }}
                className="flex w-full items-center justify-between px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                {p.label}
                {isSelected && <Check className="h-4 w-4 text-primary-600" />}
              </button>
            )
          })}
          <div className="mt-1 flex items-center gap-2 border-t border-slate-100 px-3 pt-2 dark:border-slate-800">
            <Input
              type="date"
              value={customFrom}
              onChange={(e) => setCustomFrom(e.target.value)}
              className="h-8 text-xs"
            />
            <span className="text-slate-400">–</span>
            <Input type="date" value={customTo} onChange={(e) => setCustomTo(e.target.value)} className="h-8 text-xs" />
            <button
              type="button"
              disabled={!customFrom || !customTo || customFrom > customTo}
              onClick={() => {
                onChange({ from: customFrom, to: customTo })
                setIsOpen(false)
              }}
              className="shrink-0 rounded-md bg-primary-600 px-2 py-1 text-xs font-medium text-white hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Go
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
