import { clsx } from 'clsx'
import { Card } from './Card'

/** Base pulsing placeholder bar/block - every other skeleton shape below is built from this. */
export function Skeleton({ className }: { className?: string }) {
  return <div className={clsx('animate-pulse rounded-md bg-slate-100 dark:bg-slate-800', className)} />
}

/** Matches StatTile's icon-chip + label-bar + value-bar shape. Render `count` of these in the
 * same grid classes the real StatTile row uses, so the placeholder holds the same layout. */
export function StatTileSkeleton() {
  return (
    <Card className="flex items-center gap-3 p-4">
      <Skeleton className="h-9 w-9 shrink-0 rounded-lg" />
      <div className="flex-1">
        <Skeleton className="mb-2 h-3 w-16" />
        <Skeleton className="h-5 w-20" />
      </div>
    </Card>
  )
}

/** Matches the header-row + N-row table shape shared by every list page (Inventory, Sales,
 * Customers, Suppliers, Employees, Branches, Audit Logs, Expenses). `columns` sets how many
 * `<td>`-shaped bars each row gets; `leadingThumbnail` adds a small square before the first
 * column's text (Inventory's product-image cell). */
export function TableSkeleton({
  columns = 4,
  rows = 5,
  leadingThumbnail = false,
}: {
  columns?: number
  rows?: number
  leadingThumbnail?: boolean
}) {
  return (
    <div className="p-4">
      <div className="flex flex-col gap-4">
        {Array.from({ length: rows }).map((_, row) => (
          <div key={row} className="flex items-center gap-4">
            {leadingThumbnail && <Skeleton className="h-9 w-9 shrink-0 rounded-lg" />}
            {Array.from({ length: columns }).map((_, col) => (
              <Skeleton key={col} className={clsx('h-4 flex-1', col === 0 ? 'max-w-40' : 'max-w-24')} />
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

/** Matches a Settings form section: `fields` label+input bar pairs, then a save-button-shaped
 * bar aligned right. */
export function FormSkeleton({ fields = 3 }: { fields?: number }) {
  return (
    <div className="flex flex-col gap-4">
      {Array.from({ length: fields }).map((_, i) => (
        <div key={i}>
          <Skeleton className="mb-1.5 h-3 w-24" />
          <Skeleton className="h-10 w-full" />
        </div>
      ))}
      <div className="flex justify-end">
        <Skeleton className="h-9 w-32" />
      </div>
    </div>
  )
}

/** Placeholder for a Recharts line/area chart card - an axis-line silhouette plus a soft
 * shimmering block standing in for the plotted area, at roughly the real chart's height. */
export function ChartSkeleton({ height = 280 }: { height?: number }) {
  return (
    <div className="flex flex-col gap-2" style={{ height }}>
      <Skeleton className="h-full w-full rounded-lg" />
      <div className="flex justify-between">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-2.5 w-10" />
        ))}
      </div>
    </div>
  )
}

/** Matches a row list with an optional leading icon/avatar circle, a name + subtext pair, and
 * trailing meta text - Sessions' device list, Roles' role list, Dashboard's top-products/
 * recent-transactions lists. */
export function ListSkeleton({ rows = 4, withAvatar = false }: { rows?: number; withAvatar?: boolean }) {
  return (
    <ul className="divide-y divide-slate-100 dark:divide-slate-800">
      {Array.from({ length: rows }).map((_, i) => (
        <li key={i} className="flex items-center gap-3 py-2.5">
          {withAvatar && <Skeleton className="h-9 w-9 shrink-0 rounded-full" />}
          <div className="flex-1">
            <Skeleton className="mb-1.5 h-3.5 w-32" />
            <Skeleton className="h-3 w-20" />
          </div>
          <Skeleton className="h-3.5 w-16" />
        </li>
      ))}
    </ul>
  )
}
