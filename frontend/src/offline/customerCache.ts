import { getOfflineDb } from './db'
import type { Customer } from '@/types/customer'

export async function cacheCustomers(businessId: string, customers: Customer[]): Promise<void> {
  const db = await getOfflineDb(businessId)
  const tx = db.transaction('customers', 'readwrite')
  await tx.store.clear()
  await Promise.all(customers.map((c) => tx.store.put(c)))
  await tx.done
}

export async function getCachedCustomers(businessId: string): Promise<Customer[]> {
  const db = await getOfflineDb(businessId)
  const customers = await db.getAll('customers')
  return customers.sort((a, b) => a.name.localeCompare(b.name))
}
