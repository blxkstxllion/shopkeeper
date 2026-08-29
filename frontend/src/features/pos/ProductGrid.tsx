import { Link } from 'react-router-dom'
import { Package, PackagePlus } from 'lucide-react'
import { formatMoney, resolveUploadUrl } from '@/lib/format'
import type { SellableProduct } from '@/types/sale'

export function ProductGrid({
  products,
  onSelect,
  hasActiveFilter = false,
}: {
  products: SellableProduct[]
  onSelect: (product: SellableProduct) => void
  /** Whether the empty result is from a search/category filter, vs. a genuinely empty
   * catalog - the two need different empty states (refine your search vs. add a product). */
  hasActiveFilter?: boolean
}) {
  if (products.length === 0) {
    if (hasActiveFilter) {
      return <p className="col-span-full py-16 text-center text-sm text-slate-400">No products match your search.</p>
    }
    return (
      <div className="col-span-full flex flex-col items-center gap-3 py-16 text-center">
        <PackagePlus className="h-8 w-8 text-slate-300 dark:text-slate-600" />
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Nothing to sell yet — add your first product to get started.
        </p>
        <Link
          to="/app/inventory"
          className="inline-flex items-center justify-center rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700"
        >
          Add a product
        </Link>
      </div>
    )
  }

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
      {products.map((p) => {
        const outOfStock = p.trackInventory && (p.quantityOnHand ?? 0) <= 0
        return (
          <button
            key={p.productId}
            type="button"
            disabled={outOfStock}
            onClick={() => onSelect(p)}
            className="flex flex-col items-start gap-2 rounded-xl border border-slate-200 bg-white p-3 text-left transition-colors hover:border-primary-400 hover:shadow-sm disabled:cursor-not-allowed disabled:opacity-40 dark:border-slate-700 dark:bg-slate-900"
          >
            <div className="flex h-16 w-full items-center justify-center rounded-lg bg-slate-100 text-slate-300 dark:bg-slate-800">
              {p.imageUrl ? (
                <img
                  src={resolveUploadUrl(p.imageUrl)}
                  alt={p.name}
                  className="h-full w-full rounded-lg object-cover"
                />
              ) : (
                <Package className="h-6 w-6" />
              )}
            </div>
            <div className="w-full">
              <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">{p.name}</p>
              <p className="text-xs text-slate-400">{p.sku}</p>
            </div>
            <div className="flex w-full items-center justify-between">
              <span className="text-sm font-semibold text-primary-700 dark:text-primary-400">
                {formatMoney(p.sellingPrice)}
              </span>
              {p.trackInventory && (
                <span className={outOfStock ? 'text-xs font-medium text-red-500' : 'text-xs text-slate-400'}>
                  {outOfStock ? 'Out of stock' : `${p.quantityOnHand} left`}
                </span>
              )}
            </div>
          </button>
        )
      })}
    </div>
  )
}
