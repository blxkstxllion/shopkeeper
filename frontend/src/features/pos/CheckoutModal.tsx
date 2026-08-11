import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { Banknote, CreditCard, Smartphone, Trash2 } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { formatMoney } from '@/lib/format'
import { createSale } from '@/api/sales'
import type { ApiErrorPayload } from '@/types/auth'
import type { PaymentMethod, Sale } from '@/types/sale'
import { cartLineDiscountTotal, cartSubtotal, type CartLine } from './cart'

interface PaymentRow {
  method: PaymentMethod
  amount: number
  referenceNumber: string
}

const methodConfig: Record<PaymentMethod, { label: string; icon: typeof Banknote }> = {
  Cash: { label: 'Cash', icon: Banknote },
  Card: { label: 'Card', icon: CreditCard },
  MobileMoney: { label: 'Mobile Money', icon: Smartphone },
}

export function CheckoutModal({
  isOpen,
  onClose,
  lines,
  discountAmount,
  branchId,
  onSuccess,
}: {
  isOpen: boolean
  onClose: () => void
  lines: CartLine[]
  discountAmount: number
  branchId: string
  onSuccess: (sale: Sale) => void
}) {
  const queryClient = useQueryClient()
  const [payments, setPayments] = useState<PaymentRow[]>([])
  const [serverError, setServerError] = useState<string | null>(null)

  const subtotal = cartSubtotal(lines)
  const lineDiscounts = cartLineDiscountTotal(lines)
  const total = Math.max(subtotal - lineDiscounts - discountAmount, 0)
  const paid = payments.reduce((sum, p) => sum + p.amount, 0)
  const remaining = Math.round((total - paid) * 100) / 100

  const mutation = useMutation({
    mutationFn: () =>
      createSale({
        branchId,
        items: lines.map((l) => ({ productId: l.product.productId, quantity: l.quantity, discountAmount: l.discountAmount })),
        discountAmount,
        payments: payments.map((p) => ({ method: p.method, amount: p.amount, referenceNumber: p.referenceNumber || null })),
      }),
    onSuccess: (sale) => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      queryClient.invalidateQueries({ queryKey: ['sales'] })
      setPayments([])
      onSuccess(sale)
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to complete the sale. Please try again.')
    },
  })

  function addPaymentMethod(method: PaymentMethod) {
    setPayments((prev) => [...prev, { method, amount: Math.max(remaining, 0), referenceNumber: '' }])
  }

  function updateAmount(index: number, amount: number) {
    setPayments((prev) => prev.map((p, i) => (i === index ? { ...p, amount } : p)))
  }

  function updateReference(index: number, referenceNumber: string) {
    setPayments((prev) => prev.map((p, i) => (i === index ? { ...p, referenceNumber } : p)))
  }

  function removePayment(index: number) {
    setPayments((prev) => prev.filter((_, i) => i !== index))
  }

  function handleClose() {
    setPayments([])
    setServerError(null)
    onClose()
  }

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Complete sale">
      <div className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="rounded-xl bg-slate-50 p-4 text-center dark:bg-slate-800">
          <p className="text-xs uppercase tracking-wide text-slate-400">Total due</p>
          <p className="text-2xl font-bold text-slate-900 dark:text-slate-100">{formatMoney(total)}</p>
        </div>

        <div className="grid grid-cols-3 gap-2">
          {(Object.keys(methodConfig) as PaymentMethod[]).map((method) => {
            const { label, icon: Icon } = methodConfig[method]
            return (
              <button
                key={method}
                type="button"
                onClick={() => addPaymentMethod(method)}
                disabled={remaining <= 0}
                className="flex flex-col items-center gap-1.5 rounded-xl border border-slate-300 py-3 text-xs font-medium text-slate-600 transition-colors hover:border-primary-400 hover:text-primary-700 disabled:cursor-not-allowed disabled:opacity-40 dark:border-slate-600 dark:text-slate-300"
              >
                <Icon className="h-5 w-5" />
                {label}
              </button>
            )
          })}
        </div>

        {payments.length > 0 && (
          <ul className="flex flex-col gap-2">
            {payments.map((p, i) => (
              <li key={i} className="flex items-center gap-2 rounded-lg border border-slate-200 p-2 dark:border-slate-700">
                <span className="w-28 text-sm text-slate-600 dark:text-slate-300">{methodConfig[p.method].label}</span>
                <Input
                  type="number"
                  step="0.01"
                  className="h-8"
                  value={p.amount}
                  onChange={(e) => updateAmount(i, Number(e.target.value) || 0)}
                />
                {p.method !== 'Cash' && (
                  <Input
                    placeholder="Reference #"
                    className="h-8"
                    value={p.referenceNumber}
                    onChange={(e) => updateReference(i, e.target.value)}
                  />
                )}
                <button type="button" onClick={() => removePayment(i)} className="text-slate-300 hover:text-red-500">
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className={`flex items-center justify-between text-sm font-medium ${remaining === 0 ? 'text-primary-700 dark:text-primary-400' : 'text-slate-500'}`}>
          <span>Remaining</span>
          <span>{formatMoney(Math.max(remaining, 0))}</span>
        </div>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            type="button"
            disabled={remaining !== 0 || payments.length === 0}
            isLoading={mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            Confirm payment
          </Button>
        </div>
      </div>
    </Modal>
  )
}
