import { useSyncExternalStore } from 'react'

function subscribe(callback: () => void) {
  window.addEventListener('online', callback)
  window.addEventListener('offline', callback)
  return () => {
    window.removeEventListener('online', callback)
    window.removeEventListener('offline', callback)
  }
}

/** navigator.onLine reflects network-adapter state, not "can actually reach the API" -
 * a real request failure is still the ground truth callers should fall back on. This is
 * the fast, cheap signal used to skip a doomed network attempt up front.
 *
 * The Offline Edition always reports true here, regardless of the OS's real network-adapter
 * state. This was a real, severe bug found via hands-on testing on a genuinely disconnected
 * machine: every consumer of this hook (useOfflineMutation's entire "queue if offline" path,
 * CheckoutModal/PosPage's sale flow, ProductFormModal's product-create flow, image uploads)
 * treats navigator.onLine === false as "can't reach the server", which is correct for the
 * SaaS build (a real remote API) but exactly backwards for this edition - the backend is a
 * local sidecar on 127.0.0.1, always reachable with zero dependency on real internet. A user
 * testing or using "the offline app" with Wi-Fi off (the expected, intended use case) would
 * have every write silently queued into IndexedDB instead of ever reaching the local
 * database - this is what "added inventory, nothing shows up" turned out to be. The splash
 * screen (see offline-edition/SplashScreen.tsx) already gates on the one real "is the backend
 * actually up" check via waitForBackend() before any of this code ever runs; past that point,
 * a genuine local failure (sidecar crashed) still surfaces correctly via isNetworkError in the
 * real request's catch block, independent of this flag. */
export function useOnlineStatus(): boolean {
  const osReportedOnline = useSyncExternalStore(
    subscribe,
    () => navigator.onLine,
    () => true,
  )

  return import.meta.env.VITE_EDITION === 'offline' ? true : osReportedOnline
}
