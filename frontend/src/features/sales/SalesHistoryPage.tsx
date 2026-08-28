import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Receipt } from 'lucide-react'
import { getSales } from '@/api/sales'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/ui/EmptyState'
import { TableSkeleton } from '@/components/ui/Skeleton'
import { formatMoney, formatDateTime } from '@/lib/format'
import type { SaleStatus } from '@/types/sale'
import { SaleDetailModal } from './SaleDetailModal'

const statusOptions: Array<SaleStatus | 'All'> = ['All', 'Completed', 'PartiallyRefunded', 'Refunded', 'Voided']

const statusTone: Record<SaleStatus, string> = {
  Completed: 'bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-300',
  Voided: 'bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400',
  PartiallyRefunded: 'bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
  Refunded: 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300',
}

export function SalesHistoryPage() {
  const [status, setStatus] = useState<SaleStatus | 'All'>('All')
  const [selectedSaleId, setSelectedSaleId] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['sales', status],
    queryFn: () => getSales({ status: status === 'All' ? undefined : status, pageSize: 100 }),
  })

  const sales = data?.items ?? []

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Sales history</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Every transaction, with void and refund available where applicable.
        </p>
      </div>

      <div className="mb-4 flex flex-wrap gap-1.5">
        {statusOptions.map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => setStatus(s)}
            className={`rounded-full px-3 py-1.5 text-xs font-medium transition-colors ${
              status === s
                ? 'bg-primary-600 text-white'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300'
            }`}
          >
            {s === 'PartiallyRefunded' ? 'Partially refunded' : s}
          </button>
        ))}
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={7} rows={8} />
        ) : sales.length === 0 ? (
          <EmptyState
            icon={Receipt}
            title="No sales yet"
            description="Sales you record at the POS will show up here."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Sale</th>
                  <th className="px-4 py-3 font-medium">Branch</th>
                  <th className="px-4 py-3 font-medium">Cashier</th>
                  <th className="px-4 py-3 font-medium text-right">Total</th>
                  <th className="px-4 py-3 font-medium text-right">Profit</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">When</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {sales.map((sale) => (
                  <tr
                    key={sale.id}
                    onClick={() => setSelectedSaleId(sale.id)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        setSelectedSaleId(sale.id)
                      }
                    }}
                    tabIndex={0}
                    role="button"
                    aria-label={`View sale ${sale.saleNumber}`}
                    className="cursor-pointer hover:bg-slate-50 focus-visible:bg-slate-50 focus-visible:outline-2 focus-visible:outline-primary-500 focus-visible:-outline-offset-2 dark:hover:bg-slate-800/50 dark:focus-visible:bg-slate-800/50"
                  >
                    <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{sale.saleNumber}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{sale.branchName}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{sale.cashierName}</td>
                    <td className="px-4 py-3 text-right text-slate-900 dark:text-slate-100">
                      {formatMoney(sale.total)}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-500 dark:text-slate-400">
                      {formatMoney(sale.grossProfit)}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusTone[sale.status]}`}>
                        {sale.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-xs text-slate-400">{formatDateTime(sale.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <SaleDetailModal saleId={selectedSaleId} onClose={() => setSelectedSaleId(null)} />
    </div>
  )
}
