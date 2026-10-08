import { Loader2, AlertTriangle } from 'lucide-react'
import { Logo } from '@/components/ui/Logo'
import { Button } from '@/components/ui/Button'
import { useLocalAuth } from './LocalAuthContext'

/** Shown while phase is 'starting' or 'backend-unreachable' - see LocalAuthContext. The .NET
 * sidecar takes a moment after Tauri spawns it (startup, EF migrations, identity-cache refresh),
 * and there's no "ready" event exposed to the frontend, only waitForBackend's polling. */
export function SplashScreen() {
  const { phase, retryBackendCheck } = useLocalAuth()

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-6 text-center dark:bg-slate-950">
      <Logo className="h-11 w-11" />
      {phase === 'backend-unreachable' ? (
        <>
          <AlertTriangle className="h-8 w-8 text-amber-500" />
          <div>
            <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">Couldn&apos;t start the app</p>
            <p className="mt-1 max-w-sm text-sm text-slate-500 dark:text-slate-400">
              The local backend didn&apos;t respond in time. Try again, or close and reopen the app if this keeps
              happening.
            </p>
          </div>
          <Button type="button" onClick={retryBackendCheck}>
            Try again
          </Button>
        </>
      ) : (
        <Loader2 className="h-6 w-6 animate-spin text-primary-600" />
      )}
    </div>
  )
}
