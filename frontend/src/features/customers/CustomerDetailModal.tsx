import { useQuery } from '@tanstack/react-query'
import { Receipt, Wallet, ShoppingBag, TrendingUp } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { StatTile } from '@/components/ui/StatTile'
import { EmptyState } from '@/components/ui/EmptyState'
import { getCustomer } from '@/api/customers'
import { getSales } from '@/api/sales'
import { formatDateTime, formatMoney } from '@/lib/format'

export function CustomerDetailModal({
  isOpen,
  onClose,
  customerId,
}: {
  isOpen: boolean
  onClose: () => void
  customerId: string | null
}) {
  const { data: detail, isLoading } = useQuery({
    queryKey: ['customer-detail', customerId],
    queryFn: () => getCustomer(customerId!),
    enabled: isOpen && Boolean(customerId),
  })

  const { data: sales } = useQuery({
    queryKey: ['customer-sales', customerId],
    queryFn: () => getSales({ customerId: customerId!, pageSize: 20 }),
    enabled: isOpen && Boolean(customerId),
  })

  if (!customerId) return null

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={detail ? detail.name : 'Customer'} size="lg">
      {isLoading || !detail ? (
        <div className="p-2 text-sm text-slate-400">Loading…</div>
      ) : (
        <div className="flex flex-col gap-5">
          <div className="grid grid-cols-3 gap-3">
            <StatTile label="Total spend" icon={Wallet} value={formatMoney(detail.totalSpend)} />
            <StatTile label="Purchases" icon={ShoppingBag} value={String(detail.purchaseCount)} />
            <StatTile label="Average sale" icon={TrendingUp} value={formatMoney(detail.averageSale)} />
          </div>

          <div>
            <h3 className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">Purchase history</h3>
            {!sales || sales.items.length === 0 ? (
              <EmptyState
                icon={Receipt}
                title="No purchases yet"
                description="Sales rung up for this customer will show up here."
              />
            ) : (
              <div className="max-h-72 overflow-y-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                      <th className="px-3 py-2 font-medium">Sale</th>
                      <th className="px-3 py-2 font-medium">Total</th>
                      <th className="px-3 py-2 font-medium">Status</th>
                      <th className="px-3 py-2 font-medium">Date</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {sales.items.map((s) => (
                      <tr key={s.id}>
                        <td className="px-3 py-2 font-medium text-slate-900 dark:text-slate-100">{s.saleNumber}</td>
                        <td className="px-3 py-2 text-slate-900 dark:text-slate-100">{formatMoney(s.total)}</td>
                        <td className="px-3 py-2 text-slate-500 dark:text-slate-400">{s.status}</td>
                        <td className="px-3 py-2 text-slate-500 dark:text-slate-400">{formatDateTime(s.createdAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}
    </Modal>
  )
}
