import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '@/api/auth'
import { dedupedRefresh } from '@/lib/api-client'
import { setAccessToken, onAccessTokenChange } from '@/lib/token-store'
import { clearOfflineDb } from '@/offline/db'
import type { AuthResult, User, UserBusiness } from '@/types/auth'
import type { Business } from '@/types/business'

/** Either a completed login, or a signal that the caller must now collect a 2FA code. */
type LoginOutcome = { requiresTwoFactor: true; challengeToken: string } | { requiresTwoFactor: false; user: User }

interface AuthContextValue {
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

    // dedupedRefresh (not authApi.refresh directly) because React.StrictMode double-invokes
    // this effect in development: two independent refresh calls would race to redeem the
    // same (rotating) refresh token, and the loser looks identical to token theft to the
    // API, which revokes the whole session - see the comment on dedupedRefresh itself.
    dedupedRefresh()
      .then((result) => {
        if (cancelled || !result) return
        setUser(result.user)
        setActiveBusinessId(resolveActiveBusiness(result.user)?.businessId ?? null)
      })
      .finally(() => {
        if (!cancelled) setIsInitializing(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

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

  const activeBusiness = useMemo(
    () => user?.businesses.find((b) => b.businessId === activeBusinessId) ?? null,
    [user, activeBusinessId],
  )

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
    ],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
