import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/contexts/AuthContext'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { isNetworkError } from '@/lib/api-client'
import { enqueueMutation } from './mutationQueue'
import { mutationRegistry } from './mutationRegistry'
import type { OfflineEntityType } from './db'

export interface OfflineMutationResult<T> {
  data: T | null
  queued: boolean
}

/** Wraps the "try online, queue on network error" pattern CheckoutModal pioneered for Sales,
 * generalized to every offline-eligible mutation via mutationRegistry. A real rejection (bad
 * input, a business-rule conflict like "SKU already exists") always surfaces immediately and
 * is never queued - only a genuine network failure (isNetworkError) queues. */
export function useOfflineMutation<TPayload>(
  entityType: OfflineEntityType,
  displaySummary: (payload: TPayload) => string,
) {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const isOnline = useOnlineStatus()
  const queryClient = useQueryClient()
  const definition = mutationRegistry[entityType]

  return useMutation({
    mutationFn: async ({ payload }: { payload: TPayload }): Promise<OfflineMutationResult<unknown>> => {
      const clientRequestId = crypto.randomUUID()

      if (isOnline) {
        try {
          const data = await definition.call(payload as never, clientRequestId)
          return { data, queued: false }
        } catch (err) {
          if (!isNetworkError(err)) throw err
        }
      }

      if (!businessId) throw new Error('No active business to queue this action under.')

      await enqueueMutation(businessId, {
        id: clientRequestId,
        entityType,
        payload,
        queuedAt: new Date().toISOString(),
        displaySummary: displaySummary(payload),
      })

      return { data: null, queued: true }
    },
    onSuccess: async (result) => {
      if (result.queued) {
        await queryClient.invalidateQueries({ queryKey: ['sync-queue-count', businessId] })
      } else {
        await definition.invalidate(queryClient)
      }
    },
    // Without this, React Query's default network-aware pausing would leave mutate() stuck
    // "pending" forever while offline instead of running mutationFn, which is exactly what
    // needs to run offline to decide whether to queue.
    networkMode: 'always',
  })
}
