import { getOfflineDb, type OutboxSaleRecord } from './db'

export async function enqueueSale(businessId: string, record: Omit<OutboxSaleRecord, 'attempts'>): Promise<void> {
  const db = await getOfflineDb(businessId)
  await db.put('outbox', { ...record, attempts: 0 })
}

/** Oldest first - sales should replay in the order they were rung up, both because
 * that's the order stock was actually taken and because it keeps sale numbers
 * roughly chronological once synced. */
export async function getQueuedSales(businessId: string): Promise<OutboxSaleRecord[]> {
  const db = await getOfflineDb(businessId)
  const all = await db.getAll('outbox')
  return all.sort((a, b) => a.queuedAt.localeCompare(b.queuedAt))
}

export async function removeQueuedSale(businessId: string, clientRequestId: string): Promise<void> {
  const db = await getOfflineDb(businessId)
  await db.delete('outbox', clientRequestId)
}

export async function recordSyncFailure(businessId: string, clientRequestId: string, error: string): Promise<void> {
  const db = await getOfflineDb(businessId)
  const tx = db.transaction('outbox', 'readwrite')
  const existing = await tx.store.get(clientRequestId)
  if (existing) {
    await tx.store.put({ ...existing, attempts: existing.attempts + 1, lastError: error })
  }
  await tx.done
}

export async function countQueuedSales(businessId: string): Promise<number> {
  const db = await getOfflineDb(businessId)
  return db.count('outbox')
}
