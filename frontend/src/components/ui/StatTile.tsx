import type { LucideIcon } from 'lucide-react'
import { Card } from './Card'

export function StatTile({
  label,
  icon: Icon,
  value,
  tone,
}: {
  label: string
  icon: LucideIcon
  value: string
  tone?: 'warning' | 'critical'
}) {
  const toneClass =
    tone === 'critical'
      ? 'text-[#d03b3b]'
      : tone === 'warning'
        ? 'text-[#8a5a00] dark:text-[#fab219]'
        : 'text-slate-900 dark:text-slate-100'
  return (
    <Card className="flex items-center gap-3 p-4">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-50 text-slate-400 dark:bg-slate-800">
        <Icon className="h-4 w-4" />
      </div>
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{label}</p>
        <p className={`text-lg font-semibold ${toneClass}`}>{value}</p>
      </div>
    </Card>
  )
}
