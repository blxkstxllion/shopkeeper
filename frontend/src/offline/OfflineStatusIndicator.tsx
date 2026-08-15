import { RefreshCw, WifiOff } from 'lucide-react'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { useOutboxCount } from './useOutboxCount'
import { useOfflineSync } from './OfflineSyncContext'

/** Lives in TopNav so it's visible from anywhere in the app, not just the POS screen -
 * a sale can be queued on /app/sell and still be sitting unsynced while the cashier is
 * looking at /app/inventory. Renders nothing in the common case (online, nothing queued). */
export function OfflineStatusIndicator() {
  const isOnline = useOnlineStatus()
  const outboxCount = useOutboxCount()
  const { syncNow, isSyncing } = useOfflineSync()

  if (isOnline && outboxCount === 0) return null

  if (!isOnline) {
    return (
      <div className="flex h-9 items-center gap-1.5 rounded-lg bg-slate-100 px-2.5 text-xs font-medium text-slate-500 dark:bg-slate-800 dark:text-slate-400">
        <WifiOff className="h-3.5 w-3.5" />
        Offline
        {outboxCount > 0 && <span>· {outboxCount} pending</span>}
      </div>
    )
  }

  return (
    <button
      type="button"
      onClick={() => void syncNow()}
      disabled={isSyncing}
      className="flex h-9 items-center gap-1.5 rounded-lg bg-amber-50 px-2.5 text-xs font-medium text-amber-700 hover:bg-amber-100 disabled:opacity-70 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50"
    >
      <RefreshCw className={`h-3.5 w-3.5 ${isSyncing ? 'animate-spin' : ''}`} />
      {isSyncing ? 'Syncing…' : `${outboxCount} pending sync${outboxCount === 1 ? '' : 's'}`}
    </button>
  )
}
