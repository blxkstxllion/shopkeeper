import type { LucideIcon } from 'lucide-react'
import { LineChart, Line, ResponsiveContainer } from 'recharts'
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

/** Tiny trend indicator drawn from real recent values - not decorative, so callers only
 * pass it when they actually have a short series backing the tile's headline number. */
function Sparkline({ points, isGood }: { points: number[]; isGood: boolean }) {
  if (points.length < 2) return null
  const data = points.map((value, i) => ({ i, value }))
  const strokeClass = isGood ? 'stroke-good dark:stroke-good-dark' : 'stroke-danger dark:stroke-danger-dark'
  return (
    <div className="mt-1 h-6 w-16 shrink-0">
      <ResponsiveContainer>
        <LineChart data={data}>
          <Line
            type="monotone"
            dataKey="value"
            className={strokeClass}
            stroke="currentColor"
            strokeWidth={1.75}
            dot={false}
            isAnimationActive
            animationDuration={600}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

export function StatTile({
  label,
  icon: Icon,
  value,
  tone,
  delta,
  sparkline,
}: {
  label: string
  icon: LucideIcon
  value: string
  tone?: 'warning' | 'critical'
  delta?: StatDelta
  /** Recent values (oldest first) to draw a tiny trend line next to the value. Optional -
   * only pass this when real historical data backs it. */
  sparkline?: number[]
}) {
  const toneClass =
    tone === 'critical'
      ? 'text-danger dark:text-danger-dark'
      : tone === 'warning'
        ? 'text-warning dark:text-warning-dark'
        : 'text-slate-900 dark:text-slate-100'
  const iconWrapClass =
    tone === 'critical'
      ? 'bg-danger/10 text-danger dark:bg-danger/15 dark:text-danger-dark'
      : tone === 'warning'
        ? 'bg-warning/10 text-warning dark:bg-warning/15 dark:text-warning-dark'
        : 'bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400'
  const deltaIsGood =
    delta?.percent !== null && delta?.percent !== undefined && delta.percent !== 'new'
      ? delta.goodDirection === 'down'
        ? delta.percent < 0
        : delta.percent > 0
      : true

  return (
    <Card className="flex items-center gap-3 rounded-2xl p-4 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md">
      <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${iconWrapClass}`}>
        <Icon className="h-[18px] w-[18px]" />
      </div>
      <div className="flex flex-1 items-center justify-between gap-2">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{label}</p>
          <p className={`text-lg font-semibold ${toneClass}`}>{value}</p>
          {delta && <DeltaLine delta={delta} />}
        </div>
        {sparkline && <Sparkline points={sparkline} isGood={deltaIsGood} />}
      </div>
    </Card>
  )
}
