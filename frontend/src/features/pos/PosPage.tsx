import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { getSellableProducts } from '@/api/sales'
import { getProductCategories } from '@/api/products'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { useAuth } from '@/contexts/AuthContext'
import {
  cacheCatalog,
  cacheCategories,
  filterCatalog,
  getCachedCatalog,
  getCachedCategories,
} from '@/offline/catalogCache'
import { Input } from '@/components/ui/Input'
import { Card } from '@/components/ui/Card'
import type { QueuedSale, Sale, SellableProduct } from '@/types/sale'
import { ProductGrid } from './ProductGrid'
import { CartPanel } from './CartPanel'
import { CheckoutModal } from './CheckoutModal'
import { ReceiptModal } from './ReceiptModal'
import type { CartLine } from './cart'

export function PosPage() {
  const { branch, isLoading: isBranchLoading } = useActiveBranch()
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const isOnline = useOnlineStatus()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState<string | undefined>(undefined)
  const [cart, setCart] = useState<CartLine[]>([])
  const [discountAmount, setDiscountAmount] = useState(0)
  const [isCheckoutOpen, setIsCheckoutOpen] = useState(false)
  const [completedSale, setCompletedSale] = useState<Sale | QueuedSale | null>(null)

  const { data: categories } = useQuery({
    queryKey: ['product-categories'],
    queryFn: async () => {
      if (isOnline) {
        try {
          const fresh = await getProductCategories()
          if (businessId) void cacheCategories(businessId, fresh)
          return fresh
        } catch (err) {
          if (!businessId) throw err
          const cached = await getCachedCategories(businessId)
          if (cached.length > 0) return cached
          throw err
        }
      }
      return businessId ? getCachedCategories(businessId) : []
    },
    // The queryFn above already decides what to do offline (read from IndexedDB) -
    // React Query's own default network-aware pausing would otherwise refuse to even
    // call it while navigator.onLine is false, which defeats that entirely.
    networkMode: 'always',
  })

  // The unfiltered (no search/category) fetch is the one cached as the offline catalog
  // snapshot - a filtered server response would only cover a slice of the catalog, which
  // isn't enough to answer a *different* search once the connection drops.
  const { data: products, isLoading } = useQuery({
    queryKey: ['sellable-products', branch?.id, search, categoryId],
    queryFn: async () => {
      const isUnfiltered = !search && !categoryId
      if (isOnline) {
        try {
          const fresh = await getSellableProducts(branch!.id, search || undefined, categoryId)
          if (isUnfiltered && businessId) void cacheCatalog(businessId, branch!.id, fresh)
          return fresh
        } catch (err) {
          if (!businessId) throw err
          const cached = await getCachedCatalog(businessId, branch!.id)
          if (cached.length > 0) return filterCatalog(cached, search, categoryId)
          throw err
        }
      }
      if (!businessId) return []
      const cached = await getCachedCatalog(businessId, branch!.id)
      return filterCatalog(cached, search, categoryId)
    },
    enabled: Boolean(branch),
    networkMode: 'always',
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

  function handleSaleComplete(sale: Sale | QueuedSale) {
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
        branchName={branch.name}
        onSuccess={handleSaleComplete}
      />
      <ReceiptModal sale={completedSale} onClose={() => setCompletedSale(null)} />
    </div>
  )
}
