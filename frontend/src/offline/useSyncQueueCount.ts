import { useQuery } from '@tanstack/react-query'
import { useAuth } from '@/contexts/AuthContext'
import { countQueuedMutations } from './mutationQueue'

/** Reactive view over the mutation queue's IndexedDB count - React Query is just the
 * notification layer here, not the source of truth. Callers that change the queue (enqueueing
 * or syncing a mutation) must invalidate ['sync-queue-count', businessId] themselves, the same
 * way any other IndexedDB-backed read in this app works. */
export function useSyncQueueCount(): number {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId

  const { data } = useQuery({
    queryKey: ['sync-queue-count', businessId],
    queryFn: () => (businessId ? countQueuedMutations(businessId) : 0),
    enabled: Boolean(businessId),
    networkMode: 'always', // pure IndexedDB read - there's no network involved to pause for
  })

  return data ?? 0
}
