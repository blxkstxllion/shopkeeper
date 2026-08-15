import { createContext, useContext, type ReactNode } from 'react'
import { useSyncOutbox } from './useSyncOutbox'

interface OfflineSyncValue {
  syncNow: () => Promise<void>
  isSyncing: boolean
}

const OfflineSyncContext = createContext<OfflineSyncValue | null>(null)

/** Runs useSyncOutbox exactly once for the whole app and shares it - both the
 * always-mounted auto-sync-on-reconnect behavior and the manual "sync now" button in
 * OfflineStatusIndicator need the *same* running sync, not two independent instances
 * racing each other to replay the same outbox. */
export function OfflineSyncProvider({ children }: { children: ReactNode }) {
  const sync = useSyncOutbox()
  return <OfflineSyncContext.Provider value={sync}>{children}</OfflineSyncContext.Provider>
}

export function useOfflineSync(): OfflineSyncValue {
  const ctx = useContext(OfflineSyncContext)
  if (!ctx) throw new Error('useOfflineSync must be used within an OfflineSyncProvider')
  return ctx
}
