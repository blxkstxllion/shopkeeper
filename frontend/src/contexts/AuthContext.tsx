import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '@/api/auth'
import { checkInitialSession } from '@/lib/api-client'
import { setAccessToken, onAccessTokenChange } from '@/lib/token-store'
import { clearOfflineDb } from '@/offline/db'
import { setActiveCurrencyCode } from '@/lib/format'
import { applyColorTheme } from '@/lib/colorTheme'
import { loadSessionSnapshot, saveSessionSnapshot, clearSessionSnapshot } from '@/lib/session-cache'
import type { AuthResult, User, UserBusiness } from '@/types/auth'
import type { Business } from '@/types/business'

/** Either a completed login, or a signal that the caller must now collect a 2FA code. */
type LoginOutcome = { requiresTwoFactor: true; challengeToken: string } | { requiresTwoFactor: false; user: User }

export interface AuthContextValue {
  user: User | null
  /** The business the current access token is scoped to, or null if the user hasn't picked one yet (e.g. multi-business login). */
  activeBusiness: UserBusiness | null
  isInitializing: boolean
  login: (email: string, password: string, businessId?: string) => Promise<LoginOutcome>
  completeTwoFactorLogin: (challengeToken: string, code: string) => Promise<User>
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<User>
  logout: () => Promise<void>
  selectBusiness: (businessId: string) => Promise<void>
  completeOnboarding: (business: Business & { accessToken: string }, user: User) => void
  /** Applies an already-issued AuthResult (e.g. from accepting a team invitation) - same
   * session-setting logic as login/selectBusiness, exposed for flows that get their tokens
   * from a different endpoint but should land in the app exactly the same way. */
  applyAuthResult: (result: AuthResult, businessId?: string) => void
  /** Re-pulls the current user (GET /users/me) into context without a full token refresh -
   * for flows that change a User field in place (e.g. profile photo) and need every consumer
   * of useAuth().user to see it immediately, not just after the next login/business switch. */
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function resolveActiveBusiness(user: User | null): UserBusiness | null {
  if (!user) return null
  return user.businesses.length === 1 ? user.businesses[0] : null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [activeBusinessId, setActiveBusinessId] = useState<string | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  useEffect(() => {
    let cancelled = false

    // Read the cached session first (fast - a local file/localStorage read, not a network
    // call) and render from it the moment it resolves, without waiting for the network check
    // below at all - "sign in once, stay signed in" rather than re-verifying against the
    // server on every cold start. Async because the desktop build's cache is backed by
    // tauri-plugin-store (see session-cache.ts for why), not synchronous localStorage.
    loadSessionSnapshot().then((cached) => {
      if (cancelled || !cached) return
      setUser(cached.user)
      setActiveBusinessId(cached.activeBusinessId)
      setIsInitializing(false)
    })

    // checkInitialSession (not authApi.refresh directly) because React.StrictMode
    // double-invokes this effect in development: two independent refresh calls would race
    // to redeem the same (rotating) refresh token, and the loser looks identical to token
    // theft to the API, which revokes the whole session - see dedupedRefresh's own comment.
    //
    // This always runs, even when a cache already let the app render above - it's the
    // background reconciliation that rotates the token and picks up any real change
    // (including a genuine remote logout) once connectivity is actually there.
    checkInitialSession()
      .then((outcome) => {
        if (cancelled) return
        if (outcome.kind === 'authenticated') {
          setUser(outcome.result.user)
          setActiveBusinessId(resolveActiveBusiness(outcome.result.user)?.businessId ?? null)
          return
        }
        if (outcome.kind === 'unauthenticated') {
          // The server actually said no (missing/expired/revoked refresh token) - a real
          // logout, even if a cached session was already rendered above. Not a connectivity
          // problem, so this does override the cache (the sync effect below clears it once
          // `user` goes null).
          setUser(null)
          setActiveBusinessId(null)
          return
        }
        // outcome.kind === 'network-error': couldn't reach the server to check - stay on
        // whatever was already rendered (the cached session, or signed-out if there was none).
      })
      .finally(() => {
        if (!cancelled) setIsInitializing(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  // Single place that keeps the local session snapshot in sync with whatever login,
  // selectBusiness, completeOnboarding, refreshUser, or this same mount effect just set -
  // covers every path that changes `user`/`activeBusinessId` without duplicating a save
  // call at each call site. Clears on logout (user becomes null) so a shared terminal never
  // offers up the previous cashier's cached identity on the next offline cold start.
  //
  // Gated on `!isInitializing`: `user` starts as `null` before the mount effect above has
  // resolved, which is "don't know yet," not "signed out" - without this guard, this effect
  // fires on that transient null during every cold start and wipes the very snapshot the
  // mount effect is about to try reading on a network error, before it gets the chance.
  useEffect(() => {
    if (isInitializing) return
    if (user) {
      void saveSessionSnapshot(user, activeBusinessId)
    } else {
      void clearSessionSnapshot()
    }
  }, [user, activeBusinessId, isInitializing])

  useEffect(
    () =>
      onAccessTokenChange((token) => {
        if (!token) {
          setUser(null)
          setActiveBusinessId(null)
        }
      }),
    [],
  )

  const applyAuthResult = useCallback((result: AuthResult, businessId?: string) => {
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(businessId ?? resolveActiveBusiness(result.user)?.businessId ?? null)
  }, [])

  const login = useCallback(
    async (email: string, password: string, businessId?: string): Promise<LoginOutcome> => {
      const result = await authApi.login({ email, password, businessId })

      if (result.requiresTwoFactor) {
        return { requiresTwoFactor: true, challengeToken: result.challengeToken }
      }

      applyAuthResult(result.auth, businessId)
      return { requiresTwoFactor: false, user: result.auth.user }
    },
    [applyAuthResult],
  )

  const completeTwoFactorLogin = useCallback(
    async (challengeToken: string, code: string) => {
      const result = await authApi.verifyTwoFactor(challengeToken, code)
      applyAuthResult(result)
      return result.user
    },
    [applyAuthResult],
  )

  const register = useCallback(async (email: string, password: string, firstName: string, lastName: string) => {
    const result = await authApi.register({ email, password, firstName, lastName })
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(null)
    return result.user
  }, [])

  const logout = useCallback(async () => {
    await authApi.logout().catch(() => undefined)
    // A shared/public POS terminal shouldn't keep this business's cached products and
    // customers around for whoever logs in next - offline reads must go stale, not leak.
    if (activeBusinessId) {
      await clearOfflineDb(activeBusinessId).catch(() => undefined)
    }
    setAccessToken(null)
    setUser(null)
    setActiveBusinessId(null)
  }, [activeBusinessId])

  const selectBusiness = useCallback(async (businessId: string) => {
    const result = await authApi.switchBusiness(businessId)
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(businessId)
  }, [])

  const completeOnboarding = useCallback((business: Business & { accessToken: string }, updatedUser: User) => {
    // Onboarding issues a new access token scoped to the just-created business (with its
    // real permissions) - without applying it, every request after this keeps using the
    // pre-onboarding token, which has no business context, so tenant-scoped queries
    // (branches, products, ...) silently return empty until the next full refresh.
    setAccessToken(business.accessToken)
    setUser(updatedUser)
    setActiveBusinessId(business.id)
  }, [])

  const refreshUser = useCallback(async () => {
    const freshUser = await authApi.getCurrentUser()
    setUser(freshUser)
  }, [])

  const activeBusiness = useMemo(
    () => user?.businesses.find((b) => b.businessId === activeBusinessId) ?? null,
    [user, activeBusinessId],
  )

  useEffect(() => {
    setActiveCurrencyCode(activeBusiness?.currencyCode ?? 'GHS')
    applyColorTheme(activeBusiness?.colorTheme ?? 'green')
  }, [activeBusiness])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      activeBusiness,
      isInitializing,
      login,
      completeTwoFactorLogin,
      register,
      logout,
      selectBusiness,
      completeOnboarding,
      applyAuthResult,
      refreshUser,
    }),
    [
      user,
      activeBusiness,
      isInitializing,
      login,
      completeTwoFactorLogin,
      register,
      logout,
      selectBusiness,
      completeOnboarding,
      applyAuthResult,
      refreshUser,
    ],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
