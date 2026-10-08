import type { ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from '@/contexts/AuthContext'
import { useLocalAuth } from './LocalAuthContext'

/** Bridges LocalAuthContext (PIN-based, this edition only) into the exact same AuthContext the
 * SaaS build uses. AppLayout's internals - TopNav, BranchContext, TourContext,
 * EmailVerificationBanner - all call useAuth() directly; this is what lets them be reused
 * completely unmodified instead of forked, since they're reading from the same context object,
 * just provided by a different implementation. Only ever rendered once phase is 'unlocked' (see
 * OfflineAppRouter), by which point LocalAuthContext has already fetched the real user. */
export function OfflineAuthBridge({ children }: { children: ReactNode }) {
  const { user, lock, refreshUser } = useLocalAuth()
  if (!user) return null

  const notAvailable = (action: string): never => {
    throw new Error(`${action} is not available in the offline edition.`)
  }

  const value: AuthContextValue = {
    user,
    activeBusiness: user.businesses[0] ?? null,
    isInitializing: false,
    // None of these are reachable from the offline router (no Login/Register/Onboarding/
    // SelectBusiness page is ever mounted) - thrown loudly rather than silently no-op'd so a
    // future change that accidentally wires one of them up fails fast instead of misbehaving.
    login: () => notAvailable('login'),
    completeTwoFactorLogin: () => notAvailable('completeTwoFactorLogin'),
    register: () => notAvailable('register'),
    selectBusiness: () => notAvailable('selectBusiness'),
    completeOnboarding: () => notAvailable('completeOnboarding'),
    applyAuthResult: () => notAvailable('applyAuthResult'),
    // The user-menu's only "sign out" equivalent here - there's no session to end, just the
    // PIN lock screen to show again.
    logout: async () => lock(),
    refreshUser,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
