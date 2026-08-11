import type { LucideIcon } from 'lucide-react'
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

  return (
    <div>
      <div className="flex h-6 w-full overflow-hidden rounded" role="img" aria-label="Share by category">
        {shown.map((item, i) => (
          <div
            key={item.name}
            className="h-full first:rounded-l last:rounded-r"
            style={{
              width: `${item.percentOfTotal}%`,
              backgroundColor: `var(${CATEGORY_SLOT_VARS[i % CATEGORY_SLOT_VARS.length]})`,
              marginRight: i < shown.length - 1 ? 2 : 0,
            }}
            title={`${item.name}: ${item.percentOfTotal}%`}
          />
        ))}
      </div>
      <ul className="mt-4 space-y-2.5">
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
