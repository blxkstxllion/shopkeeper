import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { createSale } from '@/api/sales'
import { isNetworkError } from '@/lib/api-client'
import { useAuth } from '@/contexts/AuthContext'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { getQueuedSales, recordSyncFailure, removeQueuedSale } from './outbox'

// After this many failed attempts, a queued sale stops being retried automatically on
// every reconnect (it's very likely a real, standing problem - e.g. stock now short -
// not a transient network blip) but it stays in the outbox for a manual retry.
const MAX_AUTO_ATTEMPTS = 5

/** Replays queued offline sales against the real API, in the order they were rung up.
 * Runs automatically the moment the app comes back online, and exposes syncNow for a
 * manual "sync now" action (see the pending-sync indicator in the POS UI). */
export function useSyncOutbox() {
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
      const queued = await getQueuedSales(businessId)
      let syncedAny = false
      for (const record of queued) {
        if (record.attempts >= MAX_AUTO_ATTEMPTS) continue
        try {
          await createSale(record.payload)
          await removeQueuedSale(businessId, record.payload.clientRequestId)
          syncedAny = true
        } catch (err) {
          if (isNetworkError(err)) {
            // Still can't reach the server - no point trying the rest of the queue
            // right now, they'll fail the same way. Leave everything for next time.
            break
          }
          const message = err instanceof Error ? err.message : 'Sync failed'
          await recordSyncFailure(businessId, record.payload.clientRequestId, message)
        }
      }
      if (syncedAny) {
        await queryClient.invalidateQueries({ queryKey: ['sellable-products'] })
        await queryClient.invalidateQueries({ queryKey: ['sales'] })
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
