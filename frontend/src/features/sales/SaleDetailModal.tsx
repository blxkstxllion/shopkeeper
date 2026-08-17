import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, RotateCcw } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { formatMoney, formatDateTime } from '@/lib/format'
import { getSale, refundSale, voidSale } from '@/api/sales'
import type { SaleStatus } from '@/types/sale'

const statusTone: Record<SaleStatus, string> = {
  Completed: 'bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-300',
  Voided: 'bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400',
  PartiallyRefunded: 'bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
  Refunded: 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300',
}

export function SaleDetailModal({ saleId, onClose }: { saleId: string | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<'view' | 'void' | 'refund'>('view')
  const [reason, setReason] = useState('')
  const [refundQuantities, setRefundQuantities] = useState<Record<string, number>>({})
  const [error, setError] = useState<string | null>(null)

  const { data: sale } = useQuery({
    queryKey: ['sale', saleId],
    queryFn: () => getSale(saleId!),
    enabled: Boolean(saleId),
  })

  const voidMutation = useMutation({
    mutationFn: () => voidSale(saleId!, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sales'] })
      queryClient.invalidateQueries({ queryKey: ['sale', saleId] })
      handleClose()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Unable to void this sale.'),
  })

  const refundMutation = useMutation({
    mutationFn: () =>
      refundSale(saleId!, {
        reason,
        items: Object.entries(refundQuantities)
          .filter(([, qty]) => qty > 0)
          .map(([saleItemId, quantity]) => ({ saleItemId, quantity })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sales'] })
      queryClient.invalidateQueries({ queryKey: ['sale', saleId] })
      handleClose()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Unable to process this refund.'),
  })

  function handleClose() {
    setMode('view')
    setReason('')
    setRefundQuantities({})
    setError(null)
    onClose()
  }

  if (!sale) return null

  const canVoid = sale.status === 'Completed'
  const canRefund = sale.status === 'Completed' || sale.status === 'PartiallyRefunded'

  return (
    <Modal isOpen={Boolean(saleId)} onClose={handleClose} title={sale.saleNumber} size="md">
      <div className="flex flex-col gap-4">
        {error && <Alert tone="error">{error}</Alert>}

        <div className="flex items-center justify-between">
          <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${statusTone[sale.status]}`}>
            {sale.status}
          </span>
          <span className="text-xs text-slate-400">{formatDateTime(sale.createdAt)}</span>
        </div>

        <div className="text-sm text-slate-500 dark:text-slate-400">
          {sale.branchName} · Cashier: {sale.cashierName}
        </div>

        <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 dark:divide-slate-800 dark:border-slate-700">
          {sale.items.map((item) => (
            <li key={item.id} className="flex flex-col gap-1 p-3 text-sm">
              <div className="flex justify-between">
                <span className="text-slate-900 dark:text-slate-100">
                  {item.quantity} × {item.productName}
                </span>
                <span className="text-slate-900 dark:text-slate-100">{formatMoney(item.lineRevenue)}</span>
              </div>
              {item.refundedQuantity > 0 && (
                <p className="text-xs text-amber-600 dark:text-amber-400">{item.refundedQuantity} unit(s) refunded</p>
              )}
              {mode === 'refund' && item.quantity - item.refundedQuantity > 0 && (
                <div className="flex items-center gap-2 pt-1">
                  <label className="text-xs text-slate-500 dark:text-slate-400">Refund qty:</label>
                  <Input
                    type="number"
                    min={0}
                    max={item.quantity - item.refundedQuantity}
                    className="h-7 w-20"
                    value={refundQuantities[item.id] ?? 0}
                    onChange={(e) =>
                      setRefundQuantities((prev) => ({ ...prev, [item.id]: Math.max(0, Number(e.target.value) || 0) }))
                    }
                  />
                  <span className="text-xs text-slate-400">of {item.quantity - item.refundedQuantity} refundable</span>
                </div>
              )}
            </li>
          ))}
        </ul>

        <div className="space-y-1 text-sm">
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
          <div className="flex justify-between text-base font-semibold text-slate-900 dark:text-slate-100">
            <span>Total</span>
            <span>{formatMoney(sale.total)}</span>
          </div>
          <div className="flex justify-between text-slate-500 dark:text-slate-400">
            <span>Gross profit</span>
            <span>{formatMoney(sale.grossProfit)}</span>
          </div>
        </div>

        {mode === 'view' && (canVoid || canRefund) && (
          <div className="flex gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
            {canRefund && (
              <Button variant="secondary" className="flex-1" onClick={() => setMode('refund')}>
                <RotateCcw className="h-4 w-4" />
                Refund
              </Button>
            )}
            {canVoid && (
              <Button variant="danger" className="flex-1" onClick={() => setMode('void')}>
                <Ban className="h-4 w-4" />
                Void sale
              </Button>
            )}
          </div>
        )}

        {(mode === 'void' || mode === 'refund') && (
          <div className="flex flex-col gap-3 border-t border-slate-100 pt-3 dark:border-slate-800">
            <Input placeholder="Reason (required)" value={reason} onChange={(e) => setReason(e.target.value)} />
            <div className="flex gap-2">
              <Button variant="ghost" className="flex-1" onClick={() => setMode('view')}>
                Cancel
              </Button>
              {mode === 'void' ? (
                <Button
                  variant="danger"
                  className="flex-1"
                  disabled={!reason.trim()}
                  isLoading={voidMutation.isPending}
                  onClick={() => voidMutation.mutate()}
                >
                  Confirm void
                </Button>
              ) : (
                <Button
                  className="flex-1"
                  disabled={!reason.trim() || Object.values(refundQuantities).every((q) => !q)}
                  isLoading={refundMutation.isPending}
                  onClick={() => refundMutation.mutate()}
                >
                  Confirm refund
                </Button>
              )}
            </div>
          </div>
        )}
      </div>
    </Modal>
  )
}
