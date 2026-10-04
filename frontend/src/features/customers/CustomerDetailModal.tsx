import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Receipt, Wallet, ShoppingBag, TrendingUp, CreditCard, History } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { StatTile } from '@/components/ui/StatTile'
import { EmptyState } from '@/components/ui/EmptyState'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { getCustomer, getCustomerLedger } from '@/api/customers'
import { getSales } from '@/api/sales'
import { formatDateTime, formatMoney } from '@/lib/format'
import { ApiError } from '@/lib/api-client'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { PaymentMethod } from '@/types/sale'
import type { RecordCustomerPaymentPayload } from '@/types/customer'

const ledgerTypeLabel: Record<string, string> = {
  Charge: 'Sold on credit',
  Payment: 'Payment received',
  RefundCredit: 'Refund applied to account',
}

export function CustomerDetailModal({
  isOpen,
  onClose,
  customerId,
}: {
  isOpen: boolean
  onClose: () => void
  customerId: string | null
}) {
  const queryClient = useQueryClient()
  const [isRecordingPayment, setIsRecordingPayment] = useState(false)
  const [paymentAmount, setPaymentAmount] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [paymentReference, setPaymentReference] = useState('')
  const [paymentError, setPaymentError] = useState<string | null>(null)

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

  const { data: ledger } = useQuery({
    queryKey: ['customer-ledger', customerId],
    queryFn: () => getCustomerLedger(customerId!, { pageSize: 20 }),
    enabled: isOpen && Boolean(customerId),
  })

  const paymentMutation = useOfflineMutation<{ customerId: string } & RecordCustomerPaymentPayload>(
    'customerPayment',
    (p) => `Payment from ${detail?.name ?? 'customer'}: ${formatMoney(p.amount)}`,
  )

  async function handleRecordPayment() {
    setPaymentError(null)
    const amount = Number(paymentAmount)
    if (!amount || amount <= 0) {
      setPaymentError('Enter an amount greater than zero.')
      return
    }
    try {
      await paymentMutation.mutateAsync({
        payload: {
          customerId: customerId!,
          amount,
          method: paymentMethod,
          referenceNumber: paymentReference || null,
        },
      })
      await queryClient.invalidateQueries({ queryKey: ['customer-detail', customerId] })
      await queryClient.invalidateQueries({ queryKey: ['customer-ledger', customerId] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      setIsRecordingPayment(false)
      setPaymentAmount('')
      setPaymentReference('')
    } catch (err) {
      setPaymentError(err instanceof ApiError ? err.message : 'Unable to record this payment. Please try again.')
    }
  }

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

          {detail.currentBalance !== 0 && (
            <div
              className={`flex items-center justify-between rounded-xl border p-4 ${
                detail.currentBalance > 0
                  ? 'border-amber-200 bg-amber-50 dark:border-amber-900/40 dark:bg-amber-900/20'
                  : 'border-primary-200 bg-primary-50 dark:border-primary-900/40 dark:bg-primary-900/20'
              }`}
            >
              <div>
                <p className="text-xs uppercase tracking-wide text-slate-400">
                  {detail.currentBalance > 0 ? 'Owes the business' : 'Credit on account'}
                </p>
                <p className="text-xl font-bold text-slate-900 dark:text-slate-100">
                  {formatMoney(Math.abs(detail.currentBalance))}
                </p>
              </div>
              <Button type="button" size="sm" onClick={() => setIsRecordingPayment(true)}>
                <CreditCard className="h-3.5 w-3.5" />
                Record payment
              </Button>
            </div>
          )}

          {isRecordingPayment && (
            <div className="flex flex-col gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-800">
              {paymentError && <Alert tone="error">{paymentError}</Alert>}
              <div className="flex gap-2">
                <div className="flex-1">
                  <FormField label="Amount" htmlFor="paymentAmount">
                    <Input
                      id="paymentAmount"
                      type="number"
                      step="0.01"
                      min="0"
                      autoFocus
                      value={paymentAmount}
                      onChange={(e) => setPaymentAmount(e.target.value)}
                    />
                  </FormField>
                </div>
                <div className="flex-1">
                  <FormField label="Method" htmlFor="paymentMethod">
                    <select
                      id="paymentMethod"
                      value={paymentMethod}
                      onChange={(e) => setPaymentMethod(e.target.value as PaymentMethod)}
                      className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                    >
                      <option value="Cash">Cash</option>
                      <option value="Card">Card</option>
                      <option value="MobileMoney">Mobile Money</option>
                    </select>
                  </FormField>
                </div>
              </div>
              {paymentMethod !== 'Cash' && (
                <Input
                  placeholder="Reference # (optional)"
                  value={paymentReference}
                  onChange={(e) => setPaymentReference(e.target.value)}
                />
              )}
              <div className="flex justify-end gap-2">
                <Button type="button" variant="ghost" size="sm" onClick={() => setIsRecordingPayment(false)}>
                  Cancel
                </Button>
                <Button
                  type="button"
                  size="sm"
                  isLoading={paymentMutation.isPending}
                  onClick={() => void handleRecordPayment()}
                >
                  Record payment
                </Button>
              </div>
            </div>
          )}

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

          {ledger && ledger.items.length > 0 && (
            <div>
              <h3 className="mb-2 flex items-center gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-300">
                <History className="h-4 w-4" />
                Account activity
              </h3>
              <div className="max-h-72 overflow-y-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                      <th className="px-3 py-2 font-medium">Activity</th>
                      <th className="px-3 py-2 font-medium">Amount</th>
                      <th className="px-3 py-2 font-medium">Balance after</th>
                      <th className="px-3 py-2 font-medium">Date</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {ledger.items.map((entry) => (
                      <tr key={entry.id}>
                        <td className="px-3 py-2 text-slate-900 dark:text-slate-100">
                          {ledgerTypeLabel[entry.type] ?? entry.type}
                          {entry.note && <span className="block text-xs text-slate-400">{entry.note}</span>}
                        </td>
                        <td
                          className={`px-3 py-2 ${entry.amount > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-primary-700 dark:text-primary-400'}`}
                        >
                          {entry.amount > 0 ? '+' : ''}
                          {formatMoney(entry.amount)}
                        </td>
                        <td className="px-3 py-2 text-slate-500 dark:text-slate-400">
                          {formatMoney(entry.balanceAfter)}
                        </td>
                        <td className="px-3 py-2 text-slate-500 dark:text-slate-400">
                          {formatDateTime(entry.createdAt)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}
    </Modal>
  )
}
