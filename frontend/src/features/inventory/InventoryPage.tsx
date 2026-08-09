import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Package, Plus, Search, SlidersHorizontal, AlertTriangle } from 'lucide-react'
import { getProducts } from '@/api/products'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { EmptyState } from '@/components/ui/EmptyState'
import { formatMoney } from '@/lib/format'
import type { Product } from '@/types/product'
import { ProductFormModal } from './ProductFormModal'
import { StockAdjustModal } from './StockAdjustModal'

export function InventoryPage() {
  const { branch } = useActiveBranch()
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [formModal, setFormModal] = useState<{ open: boolean; product?: Product | null }>({ open: false })
  const [adjustModal, setAdjustModal] = useState<Product | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['products', { search, lowStockOnly, branchId: branch?.id }],
    queryFn: () => getProducts({ search: search || undefined, lowStockOnly, branchId: branch?.id, activeOnly: true, pageSize: 100 }),
    enabled: Boolean(branch),
  })

  const products = data?.items ?? []

  return (
    <div className="mx-auto max-w-6xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Inventory</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {branch ? `Stock at ${branch.name}` : 'Loading branch…'}
          </p>
        </div>
        <Button onClick={() => setFormModal({ open: true, product: null })} disabled={!branch}>
          <Plus className="h-4 w-4" />
          Add product
        </Button>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <Input
            placeholder="Search by name, SKU, or barcode…"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <button
          type="button"
          onClick={() => setLowStockOnly((v) => !v)}
          className={`flex items-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${
            lowStockOnly
              ? 'border-amber-400 bg-amber-50 text-amber-800 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-300'
              : 'border-slate-300 text-slate-600 hover:bg-slate-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800'
          }`}
        >
          <SlidersHorizontal className="h-3.5 w-3.5" />
          Low stock only
        </button>
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <div className="p-6 text-sm text-slate-400">Loading…</div>
        ) : products.length === 0 ? (
          <EmptyState
            icon={Package}
            title="No products yet"
            description="Add your first product to start tracking inventory and profitability."
            action={
              <Button onClick={() => setFormModal({ open: true, product: null })} disabled={!branch}>
                <Plus className="h-4 w-4" />
                Add product
              </Button>
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Product</th>
                  <th className="px-4 py-3 font-medium">SKU</th>
                  <th className="px-4 py-3 font-medium text-right">Selling price</th>
                  <th className="px-4 py-3 font-medium text-right">Cost price</th>
                  <th className="px-4 py-3 font-medium text-right">Stock</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {products.map((p) => (
                  <tr key={p.id}>
                    <td className="px-4 py-3">
                      <p className="font-medium text-slate-900 dark:text-slate-100">{p.name}</p>
                      {p.categoryName && <p className="text-xs text-slate-400">{p.categoryName}</p>}
                    </td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{p.sku}</td>
                    <td className="px-4 py-3 text-right text-slate-900 dark:text-slate-100">{formatMoney(p.sellingPrice)}</td>
                    <td className="px-4 py-3 text-right text-slate-500 dark:text-slate-400">{formatMoney(p.costPrice)}</td>
                    <td className="px-4 py-3 text-right">
                      {p.trackInventory ? (
                        <span
                          className={`inline-flex items-center gap-1 font-medium ${
                            p.isLowStock ? 'text-amber-600 dark:text-amber-400' : 'text-slate-900 dark:text-slate-100'
                          }`}
                        >
                          {p.isLowStock && <AlertTriangle className="h-3.5 w-3.5" />}
                          {p.quantityOnHand ?? 0}
                        </span>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        {p.trackInventory && (
                          <Button variant="ghost" size="sm" onClick={() => setAdjustModal(p)}>
                            Adjust
                          </Button>
                        )}
                        <Button variant="ghost" size="sm" onClick={() => setFormModal({ open: true, product: p })}>
                          Edit
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <ProductFormModal
        isOpen={formModal.open}
        onClose={() => setFormModal({ open: false })}
        product={formModal.product}
        branchId={branch?.id ?? null}
      />
      <StockAdjustModal
        isOpen={Boolean(adjustModal)}
        onClose={() => setAdjustModal(null)}
        product={adjustModal}
        branchId={branch?.id ?? null}
      />
    </div>
  )
}
