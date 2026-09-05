import { getOfflineDb, type OfflineDbSchema } from './db'

// 'products' is deliberately excluded - its store is keyed by productId (not id) and branch-
// indexed, so it keeps its own dedicated cacheCatalog/getCachedCatalog in catalogCache.ts.
export type OfflineListStore =
  | 'categories'
  | 'customers'
  | 'suppliers'
  | 'expenseCategories'
  | 'expenses'
  | 'employees'
  | 'roles'
  | 'branches'
  | 'sales'

/** Replaces the cached list for one store with the given items, keyed by their `id`. */
export async function cacheList<T extends { id: string }>(
  store: OfflineListStore,
  businessId: string,
  items: T[],
): Promise<void> {
  const db = await getOfflineDb(businessId)
  const tx = db.transaction(store, 'readwrite')
  await tx.store.clear()
  await Promise.all(items.map((item) => tx.store.put(item as OfflineDbSchema[typeof store]['value'])))
  await tx.done
}

export async function getCachedList<T>(store: OfflineListStore, businessId: string): Promise<T[]> {
  const db = await getOfflineDb(businessId)
  return (await db.getAll(store)) as T[]
}

export async function cacheSingleton(key: string, businessId: string, data: unknown): Promise<void> {
  const db = await getOfflineDb(businessId)
  await db.put('singletons', { key, data, cachedAt: new Date().toISOString() })
}

export async function getCachedSingleton<T>(key: string, businessId: string): Promise<T | null> {
  const db = await getOfflineDb(businessId)
  const record = await db.get('singletons', key)
  return (record?.data as T) ?? null
}
