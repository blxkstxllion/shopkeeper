import { useQuery } from '@tanstack/react-query'
import { useAuth } from '@/contexts/AuthContext'
import { countQueuedSales } from './outbox'

/** Reactive view over the outbox's IndexedDB count - React Query is just the
 * notification layer here, not the source of truth. Callers that change the outbox
 * (enqueueing or syncing a sale) must invalidate ['outbox-count', businessId]
 * themselves, the same way any other IndexedDB-backed read in this app works. */
export function useOutboxCount(): number {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId

  const { data } = useQuery({
    queryKey: ['outbox-count', businessId],
    queryFn: () => (businessId ? countQueuedSales(businessId) : 0),
    enabled: Boolean(businessId),
    networkMode: 'always', // pure IndexedDB read - there's no network involved to pause for
  })

  return data ?? 0
}
