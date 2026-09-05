import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { isNetworkError } from '@/lib/api-client'
import { useAuth } from '@/contexts/AuthContext'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { getQueuedMutations, recordMutationSyncFailure, removeQueuedMutation } from './mutationQueue'
import { mutationRegistry } from './mutationRegistry'

// After this many failed attempts, a queued mutation stops being retried automatically on
// every reconnect (it's very likely a real, standing problem - e.g. the SKU got taken while
// it sat queued - not a transient network blip) but it stays in the queue for a manual retry
// or discard from the sync-issues panel.
const MAX_AUTO_ATTEMPTS = 5

/** Replays every queued offline mutation against the real API, in the order they were made -
 * generalizes what useSyncOutbox did for Sales specifically to every offline-eligible entity
 * type, via mutationRegistry. Runs automatically the moment the app comes back online, and
 * exposes syncNow for a manual "sync now" action (see the sync-issues panel). */
export function useSyncQueue() {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const isOnline = useOnlineStatus()
  const queryClient = useQueryClient()
  const [isSyncing, setIsSyncing] = useState(false)
  const runningRef = useRef(false)

  const syncNow = useCallback(async () => {
    if (!businessId || runningRef.current) return
    runningRef.current = true
    setIsSyncing(true)
    try {
      const queued = await getQueuedMutations(businessId)
      const syncedEntityTypes = new Set<string>()

      for (const record of queued) {
        if (record.attempts >= MAX_AUTO_ATTEMPTS) continue
        const definition = mutationRegistry[record.entityType]
        try {
          await definition.call(record.payload as never, record.id)
          await removeQueuedMutation(businessId, record.id)
          syncedEntityTypes.add(record.entityType)
        } catch (err) {
          if (isNetworkError(err)) {
            // Still can't reach the server - no point trying the rest of the queue right
            // now, they'll fail the same way. Leave everything for next time.
            break
          }
          const message = err instanceof Error ? err.message : 'Sync failed'
          await recordMutationSyncFailure(businessId, record.id, message)
        }
      }

      await queryClient.invalidateQueries({ queryKey: ['sync-queue-count', businessId] })
      for (const entityType of syncedEntityTypes) {
        await mutationRegistry[entityType as keyof typeof mutationRegistry].invalidate(queryClient)
      }
    } finally {
      runningRef.current = false
      setIsSyncing(false)
    }
  }, [businessId, queryClient])

  useEffect(() => {
    if (isOnline && businessId) void syncNow()
    // Deliberately not depending on syncNow's identity beyond mount/reconnect - this
    // should fire on the isOnline transition, not on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOnline, businessId])

  return { syncNow, isSyncing }
}
