import { useEffect, useState } from 'react'
import { useRegisterSW } from 'virtual:pwa-register/react'
import { RefreshCw, X } from 'lucide-react'

/**
 * A new service worker only takes over once every open tab reloads. On a POS
 * terminal that might be mid-sale, that reload must be the cashier's call, not
 * automatic - so this banner is the only thing that ever calls updateServiceWorker().
 */
export function UpdateAvailableBanner() {
  const [offlineReadyToastVisible, setOfflineReadyToastVisible] = useState(false)

  const {
    offlineReady: [offlineReady, setOfflineReady],
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisteredSW(_url, registration) {
      // Check for a new version on every load, not just whenever the browser feels
      // like it - a POS terminal can stay open on one tab for an entire shift.
      registration?.update()
    },
  })

  useEffect(() => {
    if (!offlineReady) return
    setOfflineReadyToastVisible(true)
    const timer = setTimeout(() => {
      setOfflineReadyToastVisible(false)
      setOfflineReady(false)
    }, 4000)
    return () => clearTimeout(timer)
  }, [offlineReady, setOfflineReady])

  if (needRefresh) {
    return (
      <div className="fixed bottom-4 left-1/2 z-50 flex w-[calc(100%-2rem)] max-w-md -translate-x-1/2 items-center gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <RefreshCw className="h-5 w-5 shrink-0 text-violet-600 dark:text-violet-400" />
        <div className="flex-1 text-sm">
          <p className="font-medium text-slate-900 dark:text-slate-100">Update available</p>
          <p className="text-slate-500 dark:text-slate-400">
            Finish any sale in progress, then reload to get the latest version.
          </p>
        </div>
        <button
          type="button"
          onClick={() => updateServiceWorker(true)}
          className="rounded-md bg-violet-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-violet-700"
        >
          Reload
        </button>
        <button
          type="button"
          onClick={() => setNeedRefresh(false)}
          aria-label="Dismiss"
          className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    )
  }

  if (offlineReadyToastVisible) {
    return (
      <div className="fixed bottom-4 left-1/2 z-50 -translate-x-1/2 rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-600 shadow-lg dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300">
        Ready to work offline.
      </div>
    )
  }

  return null
}
