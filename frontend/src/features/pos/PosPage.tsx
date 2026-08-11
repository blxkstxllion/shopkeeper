import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { getSellableProducts } from '@/api/sales'
import { getProductCategories } from '@/api/products'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { Input } from '@/components/ui/Input'
import { Card } from '@/components/ui/Card'
import type { Sale, SellableProduct } from '@/types/sale'
import { ProductGrid } from './ProductGrid'
import { CartPanel } from './CartPanel'
import { CheckoutModal } from './CheckoutModal'
import { ReceiptModal } from './ReceiptModal'
import type { CartLine } from './cart'

export function PosPage() {
  const { branch, isLoading: isBranchLoading } = useActiveBranch()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined)
  const [cart, setCart] = useState<CartLine[]>([])
  const [discountAmount, setDiscountAmount] = useState(0)
  const [isCheckoutOpen, setIsCheckoutOpen] = useState(false)
  const [completedSale, setCompletedSale] = useState<Sale | null>(null)

  const { data: categories } = useQuery({ queryKey: ['product-categories'], queryFn: getProductCategories })
  const { data: products, isLoading } = useQuery({
    queryKey: ['sellable-products', branch?.id, search, categoryId],
    queryFn: () => getSellableProducts(branch!.id, search || undefined, categoryId),
    enabled: Boolean(branch),
  })

  function handleSelectProduct(product: SellableProduct) {
    setCart((prev) => {
      const existing = prev.find((l) => l.product.productId === product.productId)
      if (existing) {
        return prev.map((l) => (l.product.productId === product.productId ? { ...l, quantity: l.quantity + 1 } : l))
      }
      return [...prev, { product, quantity: 1, discountAmount: 0 }]
    })
  }

  function handleUpdateQuantity(productId: string, quantity: number) {
    if (quantity <= 0) {
      setCart((prev) => prev.filter((l) => l.product.productId !== productId))
      return
    }
    setCart((prev) => prev.map((l) => (l.product.productId === productId ? { ...l, quantity } : l)))
  }

  function handleRemove(productId: string) {
    setCart((prev) => prev.filter((l) => l.product.productId !== productId))
  }

  function handleSaleComplete(sale: Sale) {
    setIsCheckoutOpen(false)
    setCompletedSale(sale)
    setCart([])
    setDiscountAmount(0)
  }

  if (isBranchLoading) {
    return <p className="p-6 text-sm text-slate-400">Loading…</p>
  }

  if (!branch) {
    return <p className="p-6 text-sm text-slate-400">No branch found for this business yet.</p>
  }

  return (
    <div className="flex h-[calc(100vh-8rem)] gap-4">
      <div className="flex flex-1 flex-col overflow-hidden">
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <div className="relative flex-1 min-w-[220px]">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              placeholder="Search products or scan a barcode…"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              autoFocus
            />
          </div>
          <div className="flex flex-wrap gap-1.5">
            <button
              type="button"
              onClick={() => setCategoryId(undefined)}
              className={`rounded-full px-3 py-1.5 text-xs font-medium transition-colors ${
                !categoryId
                  ? 'bg-primary-600 text-white'
                  : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300'
              }`}
            >
              All
            </button>
            {categories?.map((c) => (
              <button
                key={c.id}
                type="button"
                onClick={() => setCategoryId(c.id)}
                className={`rounded-full px-3 py-1.5 text-xs font-medium transition-colors ${
                  categoryId === c.id
                    ? 'bg-primary-600 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300'
                }`}
              >
                {c.name}
              </button>
            ))}
          </div>
        </div>

        <div className="flex-1 overflow-y-auto pr-1">
          {isLoading ? (
            <p className="p-6 text-sm text-slate-400">Loading products…</p>
          ) : (
            <ProductGrid products={products ?? []} onSelect={handleSelectProduct} />
          )}
        </div>
      </div>

      <Card className="hidden w-80 shrink-0 p-4 md:block">
        <CartPanel
          lines={cart}
          onUpdateQuantity={handleUpdateQuantity}
          onRemove={handleRemove}
          discountAmount={discountAmount}
          onDiscountChange={setDiscountAmount}
          onCharge={() => setIsCheckoutOpen(true)}
        />
      </Card>

      <CheckoutModal
        isOpen={isCheckoutOpen}
        onClose={() => setIsCheckoutOpen(false)}
        lines={cart}
        discountAmount={discountAmount}
        branchId={branch.id}
        onSuccess={handleSaleComplete}
      />
      <ReceiptModal sale={completedSale} onClose={() => setCompletedSale(null)} />
    </div>
  )
}
