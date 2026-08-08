import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'

function FullScreenLoader() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 dark:bg-slate-950">
      <Loader2 className="h-6 w-6 animate-spin text-primary-600" />
    </div>
  )
}

/** Blocks unauthenticated users; sends businesses-of-zero users to onboarding, multi-business users to the picker. */
export function RequireActiveBusiness({ children }: { children: ReactNode }) {
  const { user, activeBusiness, isInitializing } = useAuth()
  const location = useLocation()

  if (isInitializing) return <FullScreenLoader />
  if (!user) return <Navigate to="/login" replace state={{ from: location }} />
  if (user.businesses.length === 0) return <Navigate to="/onboarding" replace />
  if (!activeBusiness) return <Navigate to="/select-business" replace />

  return <>{children}</>
}

/** Blocks unauthenticated users but doesn't require a resolved business (used by onboarding/select-business). */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isInitializing } = useAuth()

  if (isInitializing) return <FullScreenLoader />
  if (!user) return <Navigate to="/login" replace />

  return <>{children}</>
}

/** Keeps already-authenticated users off the public auth pages. */
export function RedirectIfAuthed({ children }: { children: ReactNode }) {
  const { user, activeBusiness, isInitializing } = useAuth()

  if (isInitializing) return <FullScreenLoader />
  if (user) {
    if (user.businesses.length === 0) return <Navigate to="/onboarding" replace />
    if (!activeBusiness) return <Navigate to="/select-business" replace />
    return <Navigate to="/app" replace />
  }

  return <>{children}</>
}
