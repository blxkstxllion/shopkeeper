import { useEffect, useRef } from 'react'

const ACTIVITY_EVENTS = ['mousedown', 'keydown', 'touchstart', 'scroll'] as const
// Ignore additional activity within this window of the last timer reset - a burst of
// keydown/scroll events shouldn't each individually clear and reschedule two setTimeouts.
const RESET_THROTTLE_MS = 1000

/** Generic, auth-agnostic idle-detection primitive: fires onWarn `warningMs` before the full
 * `timeoutMs` of inactivity elapses, then onIdle at `timeoutMs`. Any mousedown/keydown/
 * touchstart/scroll on window resets both timers. No-ops entirely while `enabled` is false
 * (callers should pass `enabled: !!user` so idle time isn't tracked on e.g. the login page). */
export function useIdleTimer({
  enabled,
  timeoutMs,
  warningMs,
  onWarn,
  onIdle,
}: {
  enabled: boolean
  timeoutMs: number
  warningMs: number
  onWarn: () => void
  onIdle: () => void
}) {
  // Refs so a warn/idle callback identity change doesn't tear down and re-arm the listeners.
  const onWarnRef = useRef(onWarn)
  const onIdleRef = useRef(onIdle)
  onWarnRef.current = onWarn
  onIdleRef.current = onIdle

  useEffect(() => {
    if (!enabled) return

    let warnTimeout: ReturnType<typeof setTimeout>
    let idleTimeout: ReturnType<typeof setTimeout>
    let lastReset = 0

    const schedule = () => {
      clearTimeout(warnTimeout)
      clearTimeout(idleTimeout)
      warnTimeout = setTimeout(() => onWarnRef.current(), timeoutMs - warningMs)
      idleTimeout = setTimeout(() => onIdleRef.current(), timeoutMs)
    }

    const handleActivity = () => {
      const now = Date.now()
      if (now - lastReset < RESET_THROTTLE_MS) return
      lastReset = now
      schedule()
    }

    schedule()
    ACTIVITY_EVENTS.forEach((event) => window.addEventListener(event, handleActivity, { passive: true }))

    return () => {
      clearTimeout(warnTimeout)
      clearTimeout(idleTimeout)
      ACTIVITY_EVENTS.forEach((event) => window.removeEventListener(event, handleActivity))
    }
  }, [enabled, timeoutMs, warningMs])
}
