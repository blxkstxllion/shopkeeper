import { openDB, wrap, type DBSchema, type IDBPDatabase } from 'idb'
import type { CreateSalePayload, SellableProduct } from '@/types/sale'
import type { ProductCategory } from '@/types/product'
import type { Customer } from '@/types/customer'

const DB_VERSION = 3

/** Every offline-eligible mutation type - each one maps 1:1 to a backend command that opted
 * into ISupportsClientRequestId (IdempotencyBehavior). Kept as a flat union rather than a
 * generic string so a typo here is a compile error, not a silent no-op at sync time. */
export type OfflineEntityType =
  | 'sale'
  | 'refund'
  | 'void'
  | 'product'
  | 'productUpdate'
  | 'productDelete'
  | 'productCategory'
  | 'stockAdjustment'
  | 'customer'
  | 'customerUpdate'
  | 'customerDelete'
  | 'supplier'
  | 'supplierUpdate'
  | 'supplierDelete'
  | 'restock'
  | 'expense'
  | 'expenseUpdate'
  | 'expenseDelete'
  | 'expenseCategory'
  | 'employeeInvite'
  | 'employeeRemove'
  | 'joinRequestApprove'
  | 'joinRequestReject'
  | 'role'
  | 'roleUpdate'
  | 'roleDelete'
  | 'branch'
  | 'branchUpdate'
  | 'branchDelete'
  | 'businessProfile'
  | 'taxSettings'
  | 'businessAbout'

export interface QueuedMutation {
  id: string // clientRequestId
  entityType: OfflineEntityType
  payload: unknown
  queuedAt: string
  attempts: number
  lastError?: string
  /** Short human-readable label for the sync-issues panel, e.g. "New product: Blue T-Shirt" -
   * built at enqueue time since the payload shape differs per entityType. */
  displaySummary: string
}

/** A cached single-record read (dashboard summary, plan usage) - list reads use the
 * per-entity stores below instead. */
export interface CachedSingleton {
  key: string
  data: unknown
  cachedAt: string
}

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
  suppliers: { key: string; value: unknown }
  expenseCategories: { key: string; value: unknown }
  expenses: { key: string; value: unknown }
  employees: { key: string; value: unknown }
  roles: { key: string; value: unknown }
  branches: { key: string; value: unknown }
  sales: { key: string; value: unknown }
  singletons: {
    key: string
    value: CachedSingleton
  }
  mutationQueue: {
    key: string // clientRequestId
    value: QueuedMutation
  }
}

/** One database per business, not one shared database - a shared terminal that logs
 * into a different business must never surface the previous business's cached
 * products/customers while offline. See clearOfflineDb, called on logout/business
 * switch. */
function dbName(businessId: string) {
  return `shopkeeper-offline-${businessId}`
}

const LIST_CACHE_STORES = [
  'suppliers',
  'expenseCategories',
  'expenses',
  'employees',
  'roles',
  'branches',
  'sales',
] as const

const openDbs = new Map<string, Promise<IDBPDatabase<OfflineDbSchema>>>()

export function getOfflineDb(businessId: string): Promise<IDBPDatabase<OfflineDbSchema>> {
  let db = openDbs.get(businessId)
  if (!db) {
    db = openDB<OfflineDbSchema>(dbName(businessId), DB_VERSION, {
      upgrade(database, oldVersion, _newVersion, transaction) {
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
        for (const name of LIST_CACHE_STORES) {
          if (!database.objectStoreNames.contains(name)) {
            database.createObjectStore(name, { keyPath: 'id' })
          }
        }
        if (!database.objectStoreNames.contains('singletons')) {
          database.createObjectStore('singletons', { keyPath: 'key' })
        }

        if (!database.objectStoreNames.contains('mutationQueue')) {
          database.createObjectStore('mutationQueue', { keyPath: 'id' })
        }

        // v2 -> v3: the old Sales-only `outbox` store is folded into the new generic
        // mutationQueue, so a sale that was sitting queued offline through this upgrade
        // isn't silently dropped - reshaped into a QueuedMutation instead of lost.
        // 'outbox' predates OfflineDbSchema (removed from the type in this same upgrade) -
        // idb's typed database/transaction only know about current schema store names, so
        // this whole block drops to the untyped IDBDatabase/IDBTransaction on purpose.
        const rawDatabase = database as unknown as IDBDatabase
        if (oldVersion < 3 && rawDatabase.objectStoreNames.contains('outbox')) {
          const rawOldStore = (transaction as unknown as IDBTransaction).objectStore('outbox')
          const oldStore = wrap(rawOldStore) as { getAll: () => Promise<OldOutboxSaleRecord[]> }
          const newStore = transaction.objectStore('mutationQueue')
          oldStore.getAll().then((oldRecords) => {
            for (const record of oldRecords) {
              const itemCount = record.displayItems.reduce((sum, i) => sum + i.quantity, 0)
              void newStore.put({
                id: record.payload.clientRequestId,
                entityType: 'sale',
                payload: record.payload,
                queuedAt: record.queuedAt,
                attempts: record.attempts,
                lastError: record.lastError,
                displaySummary: `Sale at ${record.branchName}: ${itemCount} item${itemCount === 1 ? '' : 's'}`,
              } satisfies QueuedMutation)
            }
          })
          rawDatabase.deleteObjectStore('outbox')
        }
      },
    })
    openDbs.set(businessId, db)
  }
  return db
}

interface OldOutboxSaleRecord {
  payload: CreateSalePayload & { clientRequestId: string }
  queuedAt: string
  attempts: number
  lastError?: string
  branchName: string
  displayItems: { productId: string; productName: string; quantity: number; unitPrice: number; lineRevenue: number }[]
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
