import { useState, type FormEvent } from 'react'
import { Logo } from '@/components/ui/Logo'
import { Card } from '@/components/ui/Card'
import { Input, FormField } from '@/components/ui/Input'
import { DigitCodeInput } from '@/components/ui/DigitCodeInput'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { currencies } from '@/features/onboarding/onboarding.schema'
import { useLocalAuth } from './LocalAuthContext'

/** The entire first-run flow for this edition: store name, currency, and a 4-digit PIN - no
 * email, no password, no business type/country/tax questions. Internally reuses the same
 * onboarding business logic the SaaS wizard calls (see CompleteLocalSetupCommand on the
 * backend), just with almost every question answered by a sensible default instead of asked. */
export function SetupWizardPage() {
  const { completeSetup } = useLocalAuth()
  const [storeName, setStoreName] = useState('')
  const [currencyCode, setCurrencyCode] = useState('GHS')
  const [pin, setPin] = useState('')
  const [confirmPin, setConfirmPin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)

    if (!storeName.trim()) {
      setError('Enter your store name.')
      return
    }
    if (pin.length !== 4) {
      setError('Your PIN must be exactly 4 digits.')
      return
    }
    if (pin !== confirmPin) {
      setError("PINs don't match.")
      return
    }

    setIsSubmitting(true)
    try {
      await completeSetup(storeName.trim(), currencyCode, pin)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <Logo className="h-11 w-11" />
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">Welcome to My Shopkeeper</p>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Let&apos;s set up your store. This only takes a minute.
          </p>
        </div>

        <Card className="p-6">
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            {error && <Alert tone="error">{error}</Alert>}

            <FormField label="Store name" htmlFor="store-name">
              <Input
                id="store-name"
                autoFocus
                value={storeName}
                onChange={(e) => setStoreName(e.target.value)}
                placeholder="e.g. Ama's Shop"
                maxLength={200}
              />
            </FormField>

            <FormField label="Currency" htmlFor="currency-code">
              <select
                id="currency-code"
                value={currencyCode}
                onChange={(e) => setCurrencyCode(e.target.value)}
                className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
              >
                {currencies.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                This can&apos;t be changed later, since your prices and reports are recorded in it.
              </p>
            </FormField>

            <div>
              <p className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-200">Choose a 4-digit PIN</p>
              <DigitCodeInput length={4} value={pin} onChange={setPin} />
              <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                You&apos;ll use this to unlock the app each time you open it. Don&apos;t forget it - there&apos;s no
                recovery email.
              </p>
            </div>

            <div>
              <p className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-200">Confirm your PIN</p>
              <DigitCodeInput length={4} value={confirmPin} onChange={setConfirmPin} />
            </div>

            <Button type="submit" size="lg" isLoading={isSubmitting} className="mt-2 w-full">
              Finish setup
            </Button>
          </form>
        </Card>
      </div>
    </div>
  )
}
