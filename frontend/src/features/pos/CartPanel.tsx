import { Minus, Plus, Trash2, ShoppingCart } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { formatMoney } from '@/lib/format'
import { cartLineDiscountTotal, cartSubtotal, type CartLine } from './cart'

export function CartPanel({
  lines,
  onUpdateQuantity,
  onRemove,
  discountAmount,
  onDiscountChange,
  onCharge,
}: {
  lines: CartLine[]
  onUpdateQuantity: (productId: string, quantity: number) => void
  onRemove: (productId: string) => void
  discountAmount: number
  onDiscountChange: (value: number) => void
  onCharge: () => void
}) {
  const subtotal = cartSubtotal(lines)
  const lineDiscounts = cartLineDiscountTotal(lines)
  const total = Math.max(subtotal - lineDiscounts - discountAmount, 0)

  return (
    <div className="flex h-full flex-col">
      <div className="flex-1 overflow-y-auto">
        {lines.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-2 py-12 text-center text-slate-400">
            <ShoppingCart className="h-8 w-8" />
            <p className="text-sm">Cart is empty</p>
            <p className="text-xs">Select products to add them here.</p>
          </div>
        ) : (
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {lines.map((line) => (
              <li key={line.product.productId} className="flex items-center gap-2 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">{line.product.name}</p>
                  <p className="text-xs text-slate-400">{formatMoney(line.product.sellingPrice)} each</p>
                </div>
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    onClick={() => onUpdateQuantity(line.product.productId, line.quantity - 1)}
                    className="flex h-6 w-6 items-center justify-center rounded border border-slate-300 text-slate-500 hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-800"
                  >
                    <Minus className="h-3 w-3" />
                  </button>
                  <span className="w-6 text-center text-sm text-slate-900 dark:text-slate-100">{line.quantity}</span>
                  <button
                    type="button"
                    onClick={() => onUpdateQuantity(line.product.productId, line.quantity + 1)}
                    className="flex h-6 w-6 items-center justify-center rounded border border-slate-300 text-slate-500 hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-800"
                  >
                    <Plus className="h-3 w-3" />
                  </button>
                </div>
                <span className="w-16 text-right text-sm text-slate-900 dark:text-slate-100">
                  {formatMoney(line.product.sellingPrice * line.quantity)}
                </span>
                <button
                  type="button"
                  onClick={() => onRemove(line.product.productId)}
                  className="text-slate-300 hover:text-red-500"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="border-t border-slate-100 pt-3 dark:border-slate-800">
        <div className="mb-2 flex items-center justify-between text-sm">
          <span className="text-slate-500 dark:text-slate-400">Subtotal</span>
          <span className="text-slate-900 dark:text-slate-100">{formatMoney(subtotal)}</span>
        </div>
        <div className="mb-3 flex items-center justify-between gap-2 text-sm">
          <label htmlFor="cart-discount" className="text-slate-500 dark:text-slate-400">
            Discount
          </label>
          <Input
            id="cart-discount"
            type="number"
            min={0}
            step="0.01"
            className="h-8 w-24 text-right"
            value={discountAmount || ''}
            onChange={(e) => onDiscountChange(Number(e.target.value) || 0)}
          />
        </div>
        <div className="mb-4 flex items-center justify-between text-base font-semibold">
          <span className="text-slate-900 dark:text-slate-100">Total</span>
          <span className="text-primary-700 dark:text-primary-400">{formatMoney(total)}</span>
        </div>
        <Button className="w-full" size="lg" disabled={lines.length === 0} onClick={onCharge}>
          Charge {formatMoney(total)}
        </Button>
      </div>
    </div>
  )
}
