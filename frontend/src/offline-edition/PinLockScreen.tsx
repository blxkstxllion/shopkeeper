import { useEffect, useState } from 'react'
import { Logo } from '@/components/ui/Logo'
import { Card } from '@/components/ui/Card'
import { DigitCodeInput } from '@/components/ui/DigitCodeInput'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { useLocalAuth } from './LocalAuthContext'

export function PinLockScreen() {
  const { storeName, unlock } = useLocalAuth()
  const [pin, setPin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isChecking, setIsChecking] = useState(false)

  // Auto-submits the moment all 4 digits are entered - a PIN pad shouldn't need a separate
  // "unlock" button press on top of typing the 4th digit.
  useEffect(() => {
    if (pin.length !== 4 || isChecking) return

    let cancelled = false
    setIsChecking(true)
    setError(null)

    unlock(pin)
      .then((ok) => {
        if (cancelled) return
        if (!ok) {
          setError('Incorrect PIN. Try again.')
          setPin('')
        }
      })
      .catch((err) => {
        if (cancelled) return
        setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
        setPin('')
      })
      .finally(() => {
        if (!cancelled) setIsChecking(false)
      })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- re-running on isChecking would retry the same PIN mid-flight
  }, [pin])

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <Logo className="h-11 w-11" />
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">{storeName ?? 'My Shopkeeper'}</p>
          <p className="text-sm text-slate-500 dark:text-slate-400">Enter your PIN to continue</p>
        </div>

        <Card className="p-6">
          <div className="flex flex-col items-center gap-4">
            {error && <Alert tone="error">{error}</Alert>}
            <DigitCodeInput length={4} value={pin} onChange={setPin} error={Boolean(error)} autoFocus />
          </div>
        </Card>
      </div>
    </div>
  )
}
