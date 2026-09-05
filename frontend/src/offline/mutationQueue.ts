import { getOfflineDb, type OfflineEntityType, type QueuedMutation } from './db'

export async function enqueueMutation(businessId: string, record: Omit<QueuedMutation, 'attempts'>): Promise<void> {
  const db = await getOfflineDb(businessId)
  await db.put('mutationQueue', { ...record, attempts: 0 })
}

/** Oldest first - mutations should replay in the order they were made, both because that's
 * the order the user actually made them in and because later mutations sometimes depend on
 * earlier ones having landed (e.g. a stock adjustment against a product created moments
 * earlier in the same offline session). */
export async function getQueuedMutations(businessId: string): Promise<QueuedMutation[]> {
  const db = await getOfflineDb(businessId)
  const all = await db.getAll('mutationQueue')
  return all.sort((a, b) => a.queuedAt.localeCompare(b.queuedAt))
}

export async function getQueuedMutationsByType(
  businessId: string,
  entityType: OfflineEntityType,
): Promise<QueuedMutation[]> {
  const all = await getQueuedMutations(businessId)
  return all.filter((m) => m.entityType === entityType)
}

export async function removeQueuedMutation(businessId: string, id: string): Promise<void> {
  const db = await getOfflineDb(businessId)
  await db.delete('mutationQueue', id)
}

export async function recordMutationSyncFailure(businessId: string, id: string, error: string): Promise<void> {
  const db = await getOfflineDb(businessId)
  const tx = db.transaction('mutationQueue', 'readwrite')
  const existing = await tx.store.get(id)
  if (existing) {
    await tx.store.put({ ...existing, attempts: existing.attempts + 1, lastError: error })
  }
  await tx.done
}

export async function countQueuedMutations(businessId: string): Promise<number> {
  const db = await getOfflineDb(businessId)
  return db.count('mutationQueue')
}
