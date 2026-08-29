import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useAuth } from '@/contexts/AuthContext'
import { tourSteps } from './tourSteps'

interface TourContextValue {
  isActive: boolean
  stepIndex: number
  step: (typeof tourSteps)[number] | null
  totalSteps: number
  start: () => void
  next: () => void
  back: () => void
  skip: () => void
}

const TourContext = createContext<TourContextValue | null>(null)

function completedKey(userId: string) {
  return `shopkeeper-tour-completed:${userId}`
}

// The tour targets Sidebar nav items, which only render at lg+ (see Sidebar.tsx's `hidden
// lg:flex`) - auto-starting on a narrow viewport would spotlight elements that don't exist.
function isWideEnoughForTour() {
  return typeof window !== 'undefined' && window.matchMedia('(min-width: 1024px)').matches
}

export function TourProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const [isActive, setIsActive] = useState(false)
  const [stepIndex, setStepIndex] = useState(0)

  useEffect(() => {
    if (!user || !isWideEnoughForTour()) return
    if (localStorage.getItem(completedKey(user.id))) return

    // Let the dashboard finish its first render (nav items, data) before spotlighting anything.
    const timer = setTimeout(() => {
      setStepIndex(0)
      setIsActive(true)
    }, 700)
    return () => clearTimeout(timer)
  }, [user])

  const finish = useCallback(() => {
    setIsActive(false)
    if (user) localStorage.setItem(completedKey(user.id), '1')
  }, [user])

  const start = useCallback(() => {
    if (!isWideEnoughForTour()) return
    setStepIndex(0)
    setIsActive(true)
  }, [])

  const next = useCallback(() => {
    setStepIndex((i) => {
      if (i + 1 >= tourSteps.length) {
        finish()
        return i
      }
      return i + 1
    })
  }, [finish])

  const back = useCallback(() => {
    setStepIndex((i) => Math.max(0, i - 1))
  }, [])

  const skip = useCallback(() => finish(), [finish])

  const value = useMemo<TourContextValue>(
    () => ({
      isActive,
      stepIndex,
      step: isActive ? tourSteps[stepIndex] : null,
      totalSteps: tourSteps.length,
      start,
      next,
      back,
      skip,
    }),
    [isActive, stepIndex, start, next, back, skip],
  )

  return <TourContext.Provider value={value}>{children}</TourContext.Provider>
}

export function useTour() {
  const ctx = useContext(TourContext)
  if (!ctx) throw new Error('useTour must be used within a TourProvider')
  return ctx
}
