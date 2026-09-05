import { useEffect, useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw, WifiOff, X, AlertTriangle } from 'lucide-react'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { useAuth } from '@/contexts/AuthContext'
import { useSyncQueueCount } from './useSyncQueueCount'
import { useOfflineSync } from './OfflineSyncContext'
import { getQueuedMutations, removeQueuedMutation } from './mutationQueue'

/** Lives in TopNav so it's visible from anywhere in the app, not just the POS screen - a
 * queued mutation made on /app/sell or /app/inventory can still be sitting unsynced while the
 * cashier is looking at a completely different page. Renders nothing in the common case
 * (online, nothing queued). Click to expand the full list of queued/failed items - not just a
 * count - since a failed item's reason (e.g. "SKU already exists") needs to be visible
 * somewhere, and a bare badge has no room for that. */
export function OfflineStatusIndicator() {
  const isOnline = useOnlineStatus()
  const queueCount = useSyncQueueCount()
  const { syncNow, isSyncing } = useOfflineSync()
  const [isOpen, setIsOpen] = useState(false)
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) setIsOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  if (isOnline && queueCount === 0) return null

  return (
    <div className="relative" ref={panelRef}>
      {!isOnline ? (
        <button
          type="button"
          onClick={() => setIsOpen((o) => !o)}
          className="flex h-9 items-center gap-1.5 rounded-lg bg-slate-100 px-2.5 text-xs font-medium text-slate-500 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-400 dark:hover:bg-slate-700"
        >
          <WifiOff className="h-3.5 w-3.5" />
          Offline
          {queueCount > 0 && <span>· {queueCount} pending</span>}
        </button>
      ) : (
        <button
          type="button"
          onClick={() => setIsOpen((o) => !o)}
          disabled={isSyncing}
          className="flex h-9 items-center gap-1.5 rounded-lg bg-amber-50 px-2.5 text-xs font-medium text-amber-700 hover:bg-amber-100 disabled:opacity-70 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${isSyncing ? 'animate-spin' : ''}`} />
          {isSyncing ? 'Syncing…' : `${queueCount} pending sync${queueCount === 1 ? '' : 's'}`}
        </button>
      )}

      {isOpen && (
        <div className="absolute right-0 top-full z-30 mt-1 w-80 rounded-xl border border-slate-200 bg-white p-3 shadow-lg dark:border-slate-700 dark:bg-slate-900">
          <div className="mb-2 flex items-center justify-between">
            <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">Sync queue</p>
            {isOnline && (
              <button
                type="button"
                onClick={() => void syncNow()}
                disabled={isSyncing}
                className="text-xs font-medium text-primary-600 hover:text-primary-700 disabled:opacity-50"
              >
                {isSyncing ? 'Syncing…' : 'Sync now'}
              </button>
            )}
          </div>
          <SyncQueueList />
        </div>
      )}
    </div>
  )
}

function SyncQueueList() {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const queryClient = useQueryClient()

  const { data: items } = useQuery({
    queryKey: ['sync-queue-items', businessId],
    queryFn: () => (businessId ? getQueuedMutations(businessId) : []),
    enabled: Boolean(businessId),
    networkMode: 'always',
  })

  async function discard(id: string) {
    if (!businessId) return
    await removeQueuedMutation(businessId, id)
    await queryClient.invalidateQueries({ queryKey: ['sync-queue-count', businessId] })
    await queryClient.invalidateQueries({ queryKey: ['sync-queue-items', businessId] })
  }

  if (!items || items.length === 0) {
    return <p className="text-sm text-slate-400">Nothing queued.</p>
  }

  return (
    <ul className="flex max-h-72 flex-col gap-1.5 overflow-y-auto">
      {items.map((item) => (
        <li
          key={item.id}
          className="flex items-start justify-between gap-2 rounded-lg border border-slate-100 p-2 text-xs dark:border-slate-800"
        >
          <div className="min-w-0">
            <p className="truncate font-medium text-slate-700 dark:text-slate-200">{item.displaySummary}</p>
            {item.lastError ? (
              <p className="mt-0.5 flex items-center gap-1 text-danger dark:text-danger-dark">
                <AlertTriangle className="h-3 w-3 shrink-0" />
                <span className="truncate">{item.lastError}</span>
              </p>
            ) : (
              <p className="mt-0.5 text-slate-400">Waiting to sync…</p>
            )}
          </div>
          <button
            type="button"
            onClick={() => void discard(item.id)}
            aria-label="Discard"
            className="shrink-0 rounded p-0.5 text-slate-400 hover:bg-danger/10 hover:text-danger dark:hover:text-danger-dark"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </li>
      ))}
    </ul>
  )
}
