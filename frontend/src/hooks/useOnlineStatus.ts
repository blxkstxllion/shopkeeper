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
 * the fast, cheap signal used to skip a doomed network attempt up front. */
export function useOnlineStatus(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => navigator.onLine,
    () => true,
  )
}
