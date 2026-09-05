import { getOfflineDb } from './db'
import { cacheList, getCachedList } from './cache'
import type { SellableProduct } from '@/types/sale'
import type { ProductCategory } from '@/types/product'

/** Replaces the cached catalog for one branch. Call only with the *unfiltered* result
 * (no search/categoryId) - this is meant to hold the full sellable-product snapshot
 * offline reads filter against, not a partial search result. */
export async function cacheCatalog(businessId: string, branchId: string, products: SellableProduct[]): Promise<void> {
  const db = await getOfflineDb(businessId)
  const tx = db.transaction('products', 'readwrite')
  const existingKeys = await tx.store.index('branchId').getAllKeys(branchId)
  await Promise.all(existingKeys.map((key) => tx.store.delete(key)))
  await Promise.all(products.map((p) => tx.store.put({ ...p, branchId })))
  await tx.done
}

export async function getCachedCatalog(businessId: string, branchId: string): Promise<SellableProduct[]> {
  const db = await getOfflineDb(businessId)
  const products = await db.getAllFromIndex('products', 'branchId', branchId)
  // IndexedDB doesn't guarantee name order for a non-unique index - the backend query
  // this stands in for sorts by name, so match it explicitly.
  return products.sort((a, b) => a.name.localeCompare(b.name))
}

/** Mirrors GetSellableProductsQueryHandler's filtering exactly, so an offline search
 * behaves the same as the online one it's standing in for. */
export function filterCatalog(products: SellableProduct[], search: string | undefined, categoryId: string | undefined) {
  let result = products
  if (categoryId) {
    result = result.filter((p) => p.categoryId === categoryId)
  }
  if (search?.trim()) {
    const term = search.trim().toLowerCase()
    result = result.filter(
      (p) =>
        p.name.toLowerCase().includes(term) ||
        p.sku.toLowerCase().includes(term) ||
        (p.barcode?.toLowerCase().includes(term) ?? false),
    )
  }
  return result
}

export async function cacheCategories(businessId: string, categories: ProductCategory[]): Promise<void> {
  await cacheList('categories', businessId, categories)
}

export async function getCachedCategories(businessId: string): Promise<ProductCategory[]> {
  return getCachedList<ProductCategory>('categories', businessId)
}
