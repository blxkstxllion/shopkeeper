import type { LucideIcon } from 'lucide-react'
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts'
import { EmptyState } from './EmptyState'
import { formatMoney } from '@/lib/format'

// Fixed categorical hue order from the design system's validated 8-slot palette -
// slots are assigned by position, never generated or cycled past 8. The host page must
// define these as CSS custom properties (see DashboardPage/ReportsPage's root wrapper).
const CATEGORY_SLOT_VARS = ['--cat-0', '--cat-1', '--cat-2', '--cat-3', '--cat-4', '--cat-5', '--cat-6', '--cat-7']

export interface CategoryBreakdownItem {
  name: string
  value: number
  percentOfTotal: number
}

function Donut({ items, total }: { items: CategoryBreakdownItem[]; total: number }) {
  return (
    <div className="relative mx-auto h-44 w-44 shrink-0">
      <ResponsiveContainer>
        <PieChart>
          <Pie
            data={items}
            dataKey="value"
            nameKey="name"
            innerRadius="72%"
            outerRadius="100%"
            paddingAngle={items.length > 1 ? 2 : 0}
            startAngle={90}
            endAngle={-270}
            stroke="none"
            isAnimationActive
            animationDuration={700}
            animationEasing="ease-out"
          >
            {items.map((item, i) => (
              <Cell key={item.name} fill={`var(${CATEGORY_SLOT_VARS[i % CATEGORY_SLOT_VARS.length]})`} />
            ))}
          </Pie>
          <Tooltip
            formatter={(value, name) => [formatMoney(Number(value)), name]}
            contentStyle={{ borderRadius: 8, fontSize: 12, border: '1px solid var(--color-border)' }}
          />
        </PieChart>
      </ResponsiveContainer>
      <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
        <p className="text-base font-semibold text-slate-900 dark:text-slate-100">{formatMoney(total)}</p>
        <p className="text-xs text-slate-400">Total</p>
      </div>
    </div>
  )
}

export function CategoryBreakdown({
  items,
  emptyIcon,
  emptyTitle,
  emptyDescription,
}: {
  items: CategoryBreakdownItem[]
  emptyIcon: LucideIcon
  emptyTitle: string
  emptyDescription: string
}) {
  if (items.length === 0) {
    return <EmptyState icon={emptyIcon} title={emptyTitle} description={emptyDescription} />
  }

  const shown = items.slice(0, 8)
  const total = shown.reduce((sum, item) => sum + item.value, 0)

  return (
    <div className="flex flex-col items-center gap-5 sm:flex-row sm:items-center">
      <Donut items={shown} total={total} />
      <ul className="w-full flex-1 space-y-2.5">
        {shown.map((item, i) => (
          <li key={item.name} className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-2 text-slate-600 dark:text-slate-300">
              <span
                className="h-2.5 w-2.5 shrink-0 rounded-full"
                style={{ backgroundColor: `var(${CATEGORY_SLOT_VARS[i % CATEGORY_SLOT_VARS.length]})` }}
              />
              {item.name}
            </span>
            <span className="text-slate-900 dark:text-slate-100">
              {formatMoney(item.value)} <span className="text-slate-400">· {item.percentOfTotal}%</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
