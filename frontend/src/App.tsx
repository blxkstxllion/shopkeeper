import { useEffect } from 'react'
import { BrowserRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { queryClient } from '@/lib/query-client'
import { AuthProvider } from '@/contexts/AuthContext'
import { ThemeProvider } from '@/contexts/ThemeContext'
import { AppRouter } from '@/routes/AppRouter'
import { UpdateAvailableBanner } from '@/offline/UpdateAvailableBanner'
import { OfflineSyncProvider } from '@/offline/OfflineSyncContext'
import { IdleLogoutGuard } from '@/components/IdleLogoutGuard'

const IS_DESKTOP_APP = import.meta.env.MODE === 'tauri'

/**
 * The Tauri desktop build already ships current code to disk on every install, so
 * registering the web app's PWA service worker there only adds risk: its cache lives in
 * the WebView2 profile (%LOCALAPPDATA%/<identifier>/EBWebView), which survives an
 * uninstall/reinstall since that's a separate browser profile, not the app's install
 * directory - a stale SW can silently keep serving an old bundle forever. This unregisters
 * any SW a previous build left behind, once, so existing installs self-heal.
 */
function useDisableServiceWorkerInDesktopApp() {
  useEffect(() => {
    if (!IS_DESKTOP_APP || !('serviceWorker' in navigator)) return
    navigator.serviceWorker.getRegistrations().then((registrations) => {
      for (const registration of registrations) registration.unregister()
    })
    if ('caches' in window) {
      caches.keys().then((keys) => {
        for (const key of keys) caches.delete(key)
      })
    }
  }, [])
}

export default function App() {
  useDisableServiceWorkerInDesktopApp()

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthProvider>
            <IdleLogoutGuard />
            <OfflineSyncProvider>
              <AppRouter />
            </OfflineSyncProvider>
          </AuthProvider>
        </BrowserRouter>
        {!IS_DESKTOP_APP && <UpdateAvailableBanner />}
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  )
}
