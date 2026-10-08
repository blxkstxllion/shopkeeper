import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '@/api/auth'
import type { User } from '@/types/auth'
import * as localApi from './api'

export type LocalAuthPhase = 'starting' | 'backend-unreachable' | 'needs-setup' | 'locked' | 'unlocked'

export interface LocalAuthContextValue {
  phase: LocalAuthPhase
  storeName: string | null
  /** Only non-null once phase is 'unlocked' - fetched via GET /users/me (reused unmodified
   * from the SaaS backend, see Phase 0a's spike) so AppLayout's internals have real data to
   * read through OfflineAuthBridge, not something fabricated here that could drift from what
   * CompleteLocalSetupCommand actually created. */
  user: User | null
  completeSetup: (storeName: string, currencyCode: string, pin: string) => Promise<void>
  unlock: (pin: string) => Promise<boolean>
  lock: () => void
  refreshUser: () => Promise<void>
  /** Re-runs the backend-readiness check from the top - the only recovery path from
   * 'backend-unreachable' short of actually restarting the app. */
  retryBackendCheck: () => void
}

const LocalAuthContext = createContext<LocalAuthContextValue | null>(null)

export function LocalAuthProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<LocalAuthPhase>('starting')
  const [storeName, setStoreName] = useState<string | null>(null)
  const [user, setUser] = useState<User | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false
    setPhase('starting')

    localApi.waitForBackend().then(async (isUp) => {
      if (cancelled) return
      if (!isUp) {
        setPhase('backend-unreachable')
        return
      }

      const status = await localApi.getStatus()
      if (cancelled) return
      setStoreName(status.storeName)
      setPhase(status.setupCompleted ? 'locked' : 'needs-setup')
    })

    return () => {
      cancelled = true
    }
  }, [attempt])

  const refreshUser = useCallback(async () => {
    const freshUser = await authApi.getCurrentUser()
    setUser(freshUser)
  }, [])

  // phase only ever reaches 'unlocked' once the real user has actually been fetched - anything
  // gated on phase === 'unlocked' (see OfflineAppRouter/OfflineAuthBridge) can rely on `user`
  // already being present, no separate loading state to coordinate.
  const enterUnlocked = useCallback(async () => {
    await refreshUser()
    setPhase('unlocked')
  }, [refreshUser])

  const completeSetup = useCallback(
    async (name: string, currencyCode: string, pin: string) => {
      await localApi.completeSetup(name, currencyCode, pin)
      setStoreName(name)
      // Straight to unlocked - the owner just chose this PIN themselves, re-prompting for it
      // immediately would just be annoying, not meaningfully more secure.
      await enterUnlocked()
    },
    [enterUnlocked],
  )

  const unlock = useCallback(
    async (pin: string) => {
      const ok = await localApi.unlock(pin)
      if (ok) await enterUnlocked()
      return ok
    },
    [enterUnlocked],
  )

  const lock = useCallback(() => {
    setUser(null)
    setPhase('locked')
  }, [])

  const retryBackendCheck = useCallback(() => setAttempt((n) => n + 1), [])

  const value = useMemo<LocalAuthContextValue>(
    () => ({ phase, storeName, user, completeSetup, unlock, lock, refreshUser, retryBackendCheck }),
    [phase, storeName, user, completeSetup, unlock, lock, refreshUser, retryBackendCheck],
  )

  return <LocalAuthContext.Provider value={value}>{children}</LocalAuthContext.Provider>
}

export function useLocalAuth(): LocalAuthContextValue {
  const ctx = useContext(LocalAuthContext)
  if (!ctx) throw new Error('useLocalAuth must be used within a LocalAuthProvider')
  return ctx
}
