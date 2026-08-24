import { useCallback, useState } from 'react'
import { useAuth } from '@/contexts/AuthContext'
import { useIdleTimer } from '@/hooks/useIdleTimer'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'

// 15 minutes idle, aligned with PCI DSS's mandated re-authentication window for systems
// touching cardholder-data environments (this app processes card payments via Paystack) -
// a defensible, industry-standard number, and long enough to survive a normal lull between
// customers without interrupting an actively-used terminal.
const IDLE_TIMEOUT_MS = 15 * 60 * 1000
const IDLE_WARNING_MS = 60 * 1000

/** Rendered once near the app root, inside AuthProvider. Auto-logs-out (clearing the offline
 * cache via AuthContext.logout) after IDLE_TIMEOUT_MS of no mouse/keyboard/touch/scroll activity,
 * so a shared/public POS terminal left signed in doesn't keep a customer's cached data exposed
 * to whoever walks up next. Only tracks idle time while a user is actually logged in. */
export function IdleLogoutGuard() {
  const { user, logout } = useAuth()
  const [showWarning, setShowWarning] = useState(false)

  const handleWarn = useCallback(() => setShowWarning(true), [])
  const handleIdle = useCallback(() => {
    setShowWarning(false)
    void logout()
  }, [logout])
  // The button click itself is a window-level mousedown, so useIdleTimer's own listener already
  // resets the timer - this only needs to dismiss the modal.
  const handleStayLoggedIn = useCallback(() => setShowWarning(false), [])

  useIdleTimer({
    enabled: Boolean(user),
    timeoutMs: IDLE_TIMEOUT_MS,
    warningMs: IDLE_WARNING_MS,
    onWarn: handleWarn,
    onIdle: handleIdle,
  })

  if (!user) return null

  return (
    <Modal isOpen={showWarning} onClose={handleStayLoggedIn} title="Still there?" size="sm">
      <p className="text-sm text-slate-600 dark:text-slate-300">
        You&apos;ll be signed out in about a minute due to inactivity, and anything in the current checkout will be
        lost. Move your mouse or press a key to stay signed in.
      </p>
      <div className="mt-4 flex justify-end">
        <Button type="button" onClick={handleStayLoggedIn}>
          Stay signed in
        </Button>
      </div>
    </Modal>
  )
}
