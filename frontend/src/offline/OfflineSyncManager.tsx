import { useSyncOutbox } from './useSyncOutbox'

/** Renders nothing - just runs useSyncOutbox for the lifetime of the app, so a sale
 * queued on the POS screen still syncs even if the cashier has since navigated away
 * (e.g. to print a report) by the time the connection comes back. */
export function OfflineSyncManager() {
  useSyncOutbox()
  return null
}
