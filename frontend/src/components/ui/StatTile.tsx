import type { LucideIcon } from 'lucide-react'
import { Card } from './Card'

export interface StatDelta {
  percent: number | 'new' | null
  label: string
  /** Which direction of change counts as "good" - defaults to 'up' (e.g. revenue).
   * Pass 'down' for tiles like COGS/expenses where a decrease is the good outcome. */
  goodDirection?: 'up' | 'down'
}

function DeltaLine({ delta }: { delta: StatDelta }) {
  if (delta.percent === null) return null

  if (delta.percent === 'new') {
    return <p className="mt-0.5 text-xs font-medium text-slate-400">New {delta.label}</p>
  }

  const isGood = delta.goodDirection === 'down' ? delta.percent < 0 : delta.percent > 0
  const isBad = delta.goodDirection === 'down' ? delta.percent > 0 : delta.percent < 0
  const colorClass = isGood
    ? 'text-good dark:text-good-dark'
    : isBad
      ? 'text-danger dark:text-danger-dark'
      : 'text-slate-400'
  const sign = delta.percent > 0 ? '+' : ''

  return (
    <p className={`mt-0.5 text-xs font-medium ${colorClass}`}>
      {sign}
      {delta.percent}% {delta.label}
    </p>
  )
}

export function StatTile({
  label,
  icon: Icon,
  value,
  tone,
  delta,
}: {
  label: string
  icon: LucideIcon
  value: string
  tone?: 'warning' | 'critical'
  delta?: StatDelta
}) {
  const toneClass =
    tone === 'critical'
      ? 'text-danger dark:text-danger-dark'
      : tone === 'warning'
        ? 'text-warning dark:text-warning-dark'
        : 'text-slate-900 dark:text-slate-100'
  return (
    <Card className="flex items-center gap-3 p-4">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-50 text-slate-400 dark:bg-slate-800">
        <Icon className="h-4 w-4" />
      </div>
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{label}</p>
        <p className={`text-lg font-semibold ${toneClass}`}>{value}</p>
        {delta && <DeltaLine delta={delta} />}
      </div>
    </Card>
  )
}
