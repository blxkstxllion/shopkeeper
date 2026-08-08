import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '@/api/auth'
import { setAccessToken, onAccessTokenChange } from '@/lib/token-store'
import type { User, UserBusiness } from '@/types/auth'
import type { Business } from '@/types/business'

interface AuthContextValue {
  user: User | null
  /** The business the current access token is scoped to, or null if the user hasn't picked one yet (e.g. multi-business login). */
  activeBusiness: UserBusiness | null
  isInitializing: boolean
  login: (email: string, password: string, businessId?: string) => Promise<User>
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<User>
  logout: () => Promise<void>
  selectBusiness: (businessId: string) => Promise<void>
  completeOnboarding: (business: Business, user: User) => void
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

    authApi
      .refresh()
      .then((result) => {
        if (cancelled) return
        setAccessToken(result.accessToken)
        setUser(result.user)
        setActiveBusinessId(resolveActiveBusiness(result.user)?.businessId ?? null)
      })
      .catch(() => {
        // No valid session (first visit, expired/revoked token) - user must log in.
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

  const login = useCallback(async (email: string, password: string, businessId?: string) => {
    const result = await authApi.login({ email, password, businessId })
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(businessId ?? resolveActiveBusiness(result.user)?.businessId ?? null)
    return result.user
  }, [])

  const register = useCallback(async (email: string, password: string, firstName: string, lastName: string) => {
    const result = await authApi.register({ email, password, firstName, lastName })
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(null)
    return result.user
  }, [])

  const logout = useCallback(async () => {
    await authApi.logout().catch(() => undefined)
    setAccessToken(null)
    setUser(null)
    setActiveBusinessId(null)
  }, [])

  const selectBusiness = useCallback(async (businessId: string) => {
    const result = await authApi.switchBusiness(businessId)
    setAccessToken(result.accessToken)
    setUser(result.user)
    setActiveBusinessId(businessId)
  }, [])

  const completeOnboarding = useCallback((business: Business, updatedUser: User) => {
    setUser(updatedUser)
    setActiveBusinessId(business.id)
  }, [])

  const activeBusiness = useMemo(
    () => user?.businesses.find((b) => b.businessId === activeBusinessId) ?? null,
    [user, activeBusinessId],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ user, activeBusiness, isInitializing, login, register, logout, selectBusiness, completeOnboarding }),
    [user, activeBusiness, isInitializing, login, register, logout, selectBusiness, completeOnboarding],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
