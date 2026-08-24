import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { useSessionClaims } from '@/hooks/useSessionClaims'

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
  if (!activeBusiness) {
    // Preserves the original destination (e.g. /app/billing/callback?reference=...) through the
    // business picker - without this, any deep link that requires re-selecting a business (which
    // every full-page navigation does, since activeBusiness is in-memory only) silently drops to
    // the dashboard instead. Mirrors the ?redirect= pattern LoginPage already reads.
    const redirectTo = `${location.pathname}${location.search}`
    return <Navigate to={`/select-business?redirect=${encodeURIComponent(redirectTo)}`} replace />
  }

  return <>{children}</>
}

/** Blocks unauthenticated users but doesn't require a resolved business (used by onboarding/select-business). */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isInitializing } = useAuth()

  if (isInitializing) return <FullScreenLoader />
  if (!user) return <Navigate to="/login" replace />

  return <>{children}</>
}

/** Blocks users who lack a specific permission key (owners bypass, matching the backend's
 * ICurrentUserService.HasPermission). Redirects rather than showing a bare error page - the
 * nav item itself isn't hidden from unauthorized users yet, so this is the actual gate. */
export function RequirePermission({ permission, children }: { permission: string; children: ReactNode }) {
  const claims = useSessionClaims()

  if (claims && !claims.isOwner && !claims.permissions.includes(permission)) {
    return <Navigate to="/app" replace />
  }

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
