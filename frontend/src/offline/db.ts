import { openDB, type DBSchema, type IDBPDatabase } from 'idb'
import type { SellableProduct } from '@/types/sale'
import type { ProductCategory } from '@/types/product'
import type { Customer } from '@/types/customer'

const DB_VERSION = 1

export interface OfflineDbSchema extends DBSchema {
  products: {
    key: string // productId
    value: SellableProduct & { branchId: string }
    indexes: { branchId: string }
  }
  categories: {
    key: string // id
    value: ProductCategory
  }
  customers: {
    key: string // id
    value: Customer
  }
}

// One database per business, not one shared database - a shared terminal that logs
// into a different business must never surface the previous business's cached
// products/customers while offline. See clearOfflineDb, called on logout/business
// switch.
function dbName(businessId: string) {
  return `shopkeeper-offline-${businessId}`
}

const openDbs = new Map<string, Promise<IDBPDatabase<OfflineDbSchema>>>()

export function getOfflineDb(businessId: string): Promise<IDBPDatabase<OfflineDbSchema>> {
  let db = openDbs.get(businessId)
  if (!db) {
    db = openDB<OfflineDbSchema>(dbName(businessId), DB_VERSION, {
      upgrade(database) {
        if (!database.objectStoreNames.contains('products')) {
          const store = database.createObjectStore('products', { keyPath: 'productId' })
          store.createIndex('branchId', 'branchId')
        }
        if (!database.objectStoreNames.contains('categories')) {
          database.createObjectStore('categories', { keyPath: 'id' })
        }
        if (!database.objectStoreNames.contains('customers')) {
          database.createObjectStore('customers', { keyPath: 'id' })
        }
      },
    })
    openDbs.set(businessId, db)
  }
  return db
}

export async function clearOfflineDb(businessId: string): Promise<void> {
  const existing = openDbs.get(businessId)
  openDbs.delete(businessId)
  ;(await existing)?.close()
  await new Promise<void>((resolve) => {
    const request = indexedDB.deleteDatabase(dbName(businessId))
    request.onsuccess = () => resolve()
    request.onerror = () => resolve() // best-effort - a stale cache is a staleness bug, not a crash
    request.onblocked = () => resolve()
  })
}
