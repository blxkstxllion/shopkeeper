import { useQuery } from '@tanstack/react-query'
import { Package, AlertTriangle, PackageX, Wallet, Download } from 'lucide-react'
import { getInventoryReport } from '@/api/reports'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatTile } from '@/components/ui/StatTile'
import { EmptyState } from '@/components/ui/EmptyState'
import { UpgradePrompt } from '@/components/ui/UpgradePrompt'
import { ApiError } from '@/lib/api-client'
import { formatMoney } from '@/lib/format'
import { downloadCsv } from '@/lib/csv'
import type { DateRange } from '@/components/ui/DateRangePicker'
import type { StockAlertProduct } from '@/types/reports'

export function InventoryTab({ range, branchId }: { range: DateRange; branchId?: string }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['reports-inventory', range, branchId],
    queryFn: () => getInventoryReport({ from: range.from, to: range.to, branchId }),
  })

  if (error instanceof ApiError && error.status === 403) {
    return (
      <UpgradePrompt
        title="Reports aren't available on your current plan"
        description="Upgrade your plan to see profitability, expenses, and inventory reports."
      />
    )
  }

  if (isLoading || !data) {
    return (
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <Card key={i} className="h-24 animate-pulse p-4" />
        ))}
      </div>
    )
  }

  const { valuation } = data

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <div className="grid flex-1 grid-cols-2 gap-3 sm:grid-cols-4">
          <StatTile label="Total products" icon={Package} value={String(valuation.totalProducts)} />
          <StatTile
            label="Low stock"
            icon={AlertTriangle}
            value={String(valuation.lowStockCount)}
            tone={valuation.lowStockCount > 0 ? 'warning' : undefined}
          />
          <StatTile
            label="Out of stock"
            icon={PackageX}
            value={String(valuation.outOfStockCount)}
            tone={valuation.outOfStockCount > 0 ? 'critical' : undefined}
          />
          <StatTile label="Inventory value" icon={Wallet} value={formatMoney(valuation.inventoryValue)} />
        </div>
        <Button
          variant="secondary"
          size="sm"
          className="ml-3 shrink-0"
          onClick={() =>
            downloadCsv(
              `inventory-turnover-${range.from}-to-${range.to}.csv`,
              ['Product', 'Units sold in range', 'Quantity on hand', 'Turnover ratio'],
              data.turnover.map((t) => [t.productName, t.unitsSoldInRange, t.quantityOnHand, t.turnoverRatio ?? '']),
            )
          }
        >
          <Download className="h-3.5 w-3.5" />
          Export CSV
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <StockAlertCard title="Low stock" products={data.lowStockProducts} tone="warning" />
        <StockAlertCard title="Out of stock" products={data.outOfStockProducts} tone="critical" />
      </div>

      <Card className="p-4">
        <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Turnover</h2>
        <p className="mb-4 text-xs text-slate-400">
          Units sold in the selected range against current stock on hand - an approximation, not a true time-averaged
          turnover rate.
        </p>
        {data.turnover.length === 0 ? (
          <EmptyState icon={Package} title="No products yet" description="Add products to see turnover here." />
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                <th className="py-2 font-medium">Product</th>
                <th className="py-2 text-right font-medium">Units sold</th>
                <th className="py-2 text-right font-medium">On hand</th>
                <th className="py-2 text-right font-medium">Turnover</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {data.turnover.map((t) => (
                <tr key={t.productId}>
                  <td className="py-2.5 font-medium text-slate-900 dark:text-slate-100">{t.productName}</td>
                  <td className="py-2.5 text-right text-slate-700 dark:text-slate-300">{t.unitsSoldInRange}</td>
                  <td className="py-2.5 text-right text-slate-700 dark:text-slate-300">{t.quantityOnHand}</td>
                  <td className="py-2.5 text-right text-slate-500 dark:text-slate-400">{t.turnoverRatio ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </div>
  )
}

function StockAlertCard({
  title,
  products,
  tone,
}: {
  title: string
  products: StockAlertProduct[]
  tone: 'warning' | 'critical'
}) {
  const toneClass = tone === 'critical' ? 'text-[#d03b3b]' : 'text-[#8a5a00] dark:text-[#fab219]'
  return (
    <Card className="p-4">
      <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">{title}</h2>
      {products.length === 0 ? (
        <p className="text-sm text-slate-400">Nothing here right now.</p>
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {products.map((p) => (
            <li key={p.productId} className="flex items-center justify-between py-2 text-sm">
              <span className="text-slate-700 dark:text-slate-300">{p.productName}</span>
              <span className={`font-medium ${toneClass}`}>
                {p.quantityOnHand} / {p.reorderLevel} reorder level
              </span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
