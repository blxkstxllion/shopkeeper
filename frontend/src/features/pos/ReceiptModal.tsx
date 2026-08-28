import { CheckCircle2, Printer, RefreshCw } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { formatMoney, formatDateTime } from '@/lib/format'
import type { QueuedSale, Sale } from '@/types/sale'

export function ReceiptModal({ sale, onClose }: { sale: Sale | QueuedSale | null; onClose: () => void }) {
  if (!sale) return null

  const isQueued = 'queued' in sale
  const items = isQueued
    ? sale.items.map((i) => ({
        key: i.productId,
        quantity: i.quantity,
        productName: i.productName,
        lineRevenue: i.lineRevenue,
      }))
    : sale.items.map((i) => ({
        key: i.id,
        quantity: i.quantity,
        productName: i.productName,
        lineRevenue: i.lineRevenue,
      }))
  const payments = isQueued
    ? sale.payments.map((p, i) => ({ key: i, method: p.method, amount: p.amount }))
    : sale.payments.map((p) => ({ key: p.id, method: p.method, amount: p.amount }))

  return (
    <Modal isOpen={Boolean(sale)} onClose={onClose} title={isQueued ? 'Sale queued' : 'Sale complete'} size="sm">
      <div className="flex flex-col gap-4">
        <div className="flex flex-col items-center gap-1 py-2 text-center">
          {isQueued ? (
            <RefreshCw className="h-10 w-10 text-amber-500" />
          ) : (
            <CheckCircle2 className="h-10 w-10 text-primary-600" />
          )}
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {isQueued ? 'Will sync automatically once back online' : sale.saleNumber}
          </p>
          <p className="text-xs text-slate-400">{formatDateTime(isQueued ? sale.queuedAt : sale.createdAt)}</p>
        </div>

        <div className="rounded-xl border border-slate-200 p-3 dark:border-slate-700" id="receipt-content">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">{sale.branchName}</p>
          <ul className="divide-y divide-slate-100 text-sm dark:divide-slate-800">
            {items.map((item) => (
              <li key={item.key} className="flex justify-between py-1.5">
                <span className="text-slate-700 dark:text-slate-300">
                  {item.quantity} × {item.productName}
                </span>
                <span className="text-slate-900 dark:text-slate-100">{formatMoney(item.lineRevenue)}</span>
              </li>
            ))}
          </ul>
          <div className="mt-2 space-y-1 border-t border-slate-100 pt-2 text-sm dark:border-slate-800">
            <div className="flex justify-between text-slate-500 dark:text-slate-400">
              <span>Subtotal</span>
              <span>{formatMoney(sale.subtotal)}</span>
            </div>
            {sale.discountAmount > 0 && (
              <div className="flex justify-between text-slate-500 dark:text-slate-400">
                <span>Discount</span>
                <span>-{formatMoney(sale.discountAmount)}</span>
              </div>
            )}
            {!isQueued && sale.taxAmount > 0 && (
              <div className="flex justify-between text-slate-500 dark:text-slate-400">
                <span>Tax</span>
                <span>{formatMoney(sale.taxAmount)}</span>
              </div>
            )}
            <div className="flex justify-between text-base font-semibold text-slate-900 dark:text-slate-100">
              <span>Total</span>
              <span>{formatMoney(sale.total)}</span>
            </div>
          </div>
          <div className="mt-2 border-t border-slate-100 pt-2 text-xs text-slate-400 dark:border-slate-800">
            {payments.map((p) => (
              <div key={p.key} className="flex justify-between">
                <span>{p.method}</span>
                <span>{formatMoney(p.amount)}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="flex gap-2">
          <Button variant="secondary" className="flex-1" onClick={() => window.print()}>
            <Printer className="h-4 w-4" />
            Print
          </Button>
          <Button className="flex-1" onClick={onClose}>
            New sale
          </Button>
        </div>
      </div>
    </Modal>
  )
}
