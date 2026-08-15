import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { Banknote, CreditCard, Plus, Smartphone, Trash2 } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { formatMoney } from '@/lib/format'
import { createSale } from '@/api/sales'
import { createCustomer, getCustomers } from '@/api/customers'
import { useAuth } from '@/contexts/AuthContext'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { isNetworkError } from '@/lib/api-client'
import { cacheCustomers, getCachedCustomers } from '@/offline/customerCache'
import { enqueueSale } from '@/offline/outbox'
import type { ApiErrorPayload } from '@/types/auth'
import type { PaymentMethod, QueuedSale, Sale } from '@/types/sale'
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
  branchName,
  onSuccess,
}: {
  isOpen: boolean
  onClose: () => void
  lines: CartLine[]
  discountAmount: number
  branchId: string
  branchName: string
  onSuccess: (sale: Sale | QueuedSale) => void
}) {
  const queryClient = useQueryClient()
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const isOnline = useOnlineStatus()
  const [payments, setPayments] = useState<PaymentRow[]>([])
  const [serverError, setServerError] = useState<string | null>(null)
  const [customerId, setCustomerId] = useState('')
  const [isAddingCustomer, setIsAddingCustomer] = useState(false)
  const [newCustomerName, setNewCustomerName] = useState('')

  const { data: customers } = useQuery({
    queryKey: ['customers', { activeOnly: true, pageSize: 200 }],
    queryFn: async () => {
      if (isOnline) {
        try {
          const fresh = await getCustomers({ activeOnly: true, pageSize: 200 })
          if (businessId) void cacheCustomers(businessId, fresh.items)
          return fresh
        } catch (err) {
          if (!businessId) throw err
          const cached = await getCachedCustomers(businessId)
          if (cached.length > 0)
            return { items: cached, totalCount: cached.length, page: 1, pageSize: cached.length, totalPages: 1 }
          throw err
        }
      }
      if (!businessId) return { items: [], totalCount: 0, page: 1, pageSize: 0, totalPages: 0 }
      const cached = await getCachedCustomers(businessId)
      return { items: cached, totalCount: cached.length, page: 1, pageSize: cached.length, totalPages: 1 }
    },
    enabled: isOpen,
    // See the identical comment in PosPage.tsx's product query - the queryFn already
    // handles offline itself, so React Query's default pausing must be turned off.
    networkMode: 'always',
  })

  useEffect(() => {
    if (isOpen) {
      setCustomerId('')
      setIsAddingCustomer(false)
      setNewCustomerName('')
    }
  }, [isOpen])

  const customerMutation = useMutation({
    mutationFn: () => createCustomer({ name: newCustomerName }),
    onSuccess: async (customer) => {
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      setCustomerId(customer.id)
      setIsAddingCustomer(false)
      setNewCustomerName('')
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to add that customer. Please try again.')
    },
    // Without this, React Query's default network-aware pausing leaves mutate() stuck
    // "pending" forever while offline instead of running mutationFn (which would fail
    // fast with a real error) - adding a customer genuinely isn't supported offline,
    // but it must fail visibly, not hang.
    networkMode: 'always',
  })

  const subtotal = cartSubtotal(lines)
  const lineDiscounts = cartLineDiscountTotal(lines)
  const total = Math.max(subtotal - lineDiscounts - discountAmount, 0)
  const paid = payments.reduce((sum, p) => sum + p.amount, 0)
  const remaining = Math.round((total - paid) * 100) / 100

  const mutation = useMutation({
    mutationFn: async (): Promise<Sale | QueuedSale> => {
      const payload = {
        branchId,
        items: lines.map((l) => ({
          productId: l.product.productId,
          quantity: l.quantity,
          discountAmount: l.discountAmount,
        })),
        discountAmount,
        payments: payments.map((p) => ({
          method: p.method,
          amount: p.amount,
          referenceNumber: p.referenceNumber || null,
        })),
        customerId: customerId || null,
        // Generated fresh for every attempt, online or offline - a retry of this exact
        // payload (flaky connection, or a resync after reconnecting) can never double-sell.
        clientRequestId: crypto.randomUUID(),
      }

      if (isOnline) {
        try {
          return await createSale(payload)
        } catch (err) {
          if (!isNetworkError(err)) throw err // a real rejection (e.g. insufficient stock) must surface, not queue
        }
      }

      if (!businessId) throw new Error('No active business to queue this sale under.')

      const displayItems = lines.map((l) => ({
        productId: l.product.productId,
        productName: l.product.name,
        quantity: l.quantity,
        unitPrice: l.product.sellingPrice,
        lineRevenue: l.product.sellingPrice * l.quantity - l.discountAmount,
      }))
      const queuedAt = new Date().toISOString()
      await enqueueSale(businessId, { payload, queuedAt, branchName, displayItems })

      return {
        queued: true,
        clientRequestId: payload.clientRequestId,
        branchName,
        queuedAt,
        items: displayItems,
        subtotal,
        discountAmount: lineDiscounts + discountAmount,
        total,
        payments: payload.payments,
      }
    },
    onSuccess: (result) => {
      if ('queued' in result) {
        queryClient.invalidateQueries({ queryKey: ['outbox-count', businessId] })
      } else {
        queryClient.invalidateQueries({ queryKey: ['sellable-products'] })
        queryClient.invalidateQueries({ queryKey: ['sales'] })
      }
      setPayments([])
      onSuccess(result)
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to complete the sale. Please try again.')
    },
    // mutationFn already decides what to do offline (queue to the outbox) - without
    // this, React Query's default network-aware pausing would leave mutate() stuck
    // "pending" forever while offline instead of ever calling it.
    networkMode: 'always',
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

        <FormField label="Customer (optional)" htmlFor="customerId">
          {isAddingCustomer ? (
            <div className="flex gap-2">
              <Input
                autoFocus
                placeholder="Customer name"
                value={newCustomerName}
                onChange={(e) => setNewCustomerName(e.target.value)}
              />
              <Button
                type="button"
                size="sm"
                isLoading={customerMutation.isPending}
                disabled={!newCustomerName.trim()}
                onClick={() => customerMutation.mutate()}
              >
                Add
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setIsAddingCustomer(false)}>
                Cancel
              </Button>
            </div>
          ) : (
            <div className="flex gap-2">
              <select
                id="customerId"
                value={customerId}
                onChange={(e) => setCustomerId(e.target.value)}
                className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
              >
                <option value="">Walk-in (no customer)</option>
                {customers?.items.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              <Button type="button" variant="secondary" size="sm" onClick={() => setIsAddingCustomer(true)}>
                <Plus className="h-3.5 w-3.5" />
                New
              </Button>
            </div>
          )}
        </FormField>

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
              <li
                key={i}
                className="flex items-center gap-2 rounded-lg border border-slate-200 p-2 dark:border-slate-700"
              >
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

        <div
          className={`flex items-center justify-between text-sm font-medium ${remaining === 0 ? 'text-primary-700 dark:text-primary-400' : 'text-slate-500'}`}
        >
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
