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
import { LocalAuthProvider } from '@/offline-edition/LocalAuthContext'
import { OfflineAppRouter } from '@/offline-edition/OfflineAppRouter'

const IS_OFFLINE_EDITION = import.meta.env.VITE_EDITION === 'offline'
// Both are Tauri desktop builds that ship current code to disk on every install - see the SW
// disable hook's own comment below for why that makes the PWA service worker pure risk, no
// benefit, in either one.
const IS_DESKTOP_APP = import.meta.env.MODE === 'tauri' || IS_OFFLINE_EDITION

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

/** The SaaS tree: JWT login, multi-employee accounts, the full router. Untouched by the
 * Offline Edition split below - see OfflineAppTree for that one's equivalent. */
function SaasAppTree() {
  return (
    <AuthProvider>
      <IdleLogoutGuard />
      <OfflineSyncProvider>
        <AppRouter />
      </OfflineSyncProvider>
    </AuthProvider>
  )
}

/** No login, no JWT, no IdleLogoutGuard (it calls the SaaS AuthContext's logout - meaningless
 * with no password/session to log out of; re-locking behind the PIN after idle time is a
 * plausible future addition, not something the current PIN-setup/unlock flow needs yet).
 * OfflineSyncProvider is NOT mounted here, unlike SaasAppTree - it's nested inside
 * OfflineAppRouter's own OfflineAuthBridge instead, because useSyncQueue (which it runs)
 * calls useAuth() directly and would crash mounted any earlier than that, before a real
 * AuthContextValue exists to read. */
function OfflineAppTree() {
  return (
    <LocalAuthProvider>
      <OfflineAppRouter />
    </LocalAuthProvider>
  )
}

export default function App() {
  useDisableServiceWorkerInDesktopApp()

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>{IS_OFFLINE_EDITION ? <OfflineAppTree /> : <SaasAppTree />}</BrowserRouter>
        {!IS_DESKTOP_APP && <UpdateAvailableBanner />}
        {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </ThemeProvider>
  )
}
