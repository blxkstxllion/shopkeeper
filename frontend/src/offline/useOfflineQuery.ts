import { useQuery, type UseQueryResult } from '@tanstack/react-query'
import { useAuth } from '@/contexts/AuthContext'
import { isNetworkError } from '@/lib/api-client'
import { cacheList, getCachedList, cacheSingleton, getCachedSingleton } from './cache'
import type { OfflineListStore } from './cache'

/** Read-through cache for a list-shaped query: on success, writes the fresh list into
 * IndexedDB; on a network error, falls back to whatever was last cached (empty, if this
 * business has never loaded this screen online before). Mirrors the pattern PosPage already
 * uses for the sellable-product catalog, generalized to every other list screen - Dashboard,
 * Inventory, Customers, Suppliers, Expenses, Employees, Roles, Branches, Sales history. */
export function useOfflineListQuery<T extends { id: string }>(
  queryKey: unknown[],
  store: OfflineListStore,
  queryFn: () => Promise<T[]>,
  enabled = true,
): UseQueryResult<T[]> {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId

  return useQuery({
    queryKey,
    queryFn: async () => {
      try {
        const data = await queryFn()
        if (businessId) await cacheList(store, businessId, data)
        return data
      } catch (err) {
        if (!isNetworkError(err) || !businessId) throw err
        return getCachedList<T>(store, businessId)
      }
    },
    enabled: enabled && Boolean(businessId),
    networkMode: 'always',
  })
}

/** Same idea for a single-record read (dashboard summary, plan usage) instead of a list. */
export function useOfflineSingletonQuery<T>(
  queryKey: unknown[],
  singletonKey: string,
  queryFn: () => Promise<T>,
  enabled = true,
): UseQueryResult<T> {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId

  return useQuery({
    queryKey,
    queryFn: async () => {
      try {
        const data = await queryFn()
        if (businessId) await cacheSingleton(singletonKey, businessId, data)
        return data
      } catch (err) {
        if (!isNetworkError(err) || !businessId) throw err
        const cached = await getCachedSingleton<T>(singletonKey, businessId)
        if (cached === null) throw err // never successfully cached - nothing to fall back to
        return cached
      }
    },
    enabled: enabled && Boolean(businessId),
    networkMode: 'always',
  })
}
