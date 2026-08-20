import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import QRCode from 'qrcode'
import { useEffect } from 'react'
import { ShieldCheck, ShieldOff, Copy, Check } from 'lucide-react'
import * as authApi from '@/api/auth'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { DigitCodeInput } from '@/components/ui/DigitCodeInput'
import { Alert } from '@/components/ui/Alert'
import { Modal } from '@/components/ui/Modal'
import { ApiError } from '@/lib/api-client'

type SetupStep = 'scan' | 'recoveryCodes'

export function TwoFactorSection() {
  const queryClient = useQueryClient()
  const { data: status } = useQuery({ queryKey: ['2fa-status'], queryFn: authApi.getTwoFactorStatus })

  const [isSetupOpen, setIsSetupOpen] = useState(false)
  const [isDisableOpen, setIsDisableOpen] = useState(false)
  const [setupStep, setSetupStep] = useState<SetupStep>('scan')
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([])
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  const setupMutation = useMutation({
    mutationFn: authApi.setupTwoFactor,
    onSuccess: async (data) => {
      setQrDataUrl(await QRCode.toDataURL(data.provisioningUri))
    },
    onError: () => setError('Unable to start two-factor setup. Please try again.'),
  })

  useEffect(() => {
    if (isSetupOpen) {
      setSetupStep('scan')
      setCode('')
      setError(null)
      setupMutation.mutate()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isSetupOpen])

  const enableMutation = useMutation({
    mutationFn: () => authApi.enableTwoFactor(code),
    onSuccess: (data) => {
      setRecoveryCodes(data.recoveryCodes)
      setSetupStep('recoveryCodes')
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['2fa-status'] })
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : 'Invalid code. Please try again.')
    },
  })

  const disableMutation = useMutation({
    mutationFn: () => authApi.disableTwoFactor(password),
    onSuccess: () => {
      setIsDisableOpen(false)
      setPassword('')
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['2fa-status'] })
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : 'Incorrect password.')
    },
  })

  function closeSetup() {
    setIsSetupOpen(false)
    setQrDataUrl(null)
    setRecoveryCodes([])
  }

  return (
    <Card className="p-4">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div
            className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
              status?.enabled
                ? 'bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400'
                : 'bg-slate-100 text-slate-400 dark:bg-slate-800'
            }`}
          >
            {status?.enabled ? <ShieldCheck className="h-5 w-5" /> : <ShieldOff className="h-5 w-5" />}
          </div>
          <div>
            <h3 className="text-sm font-semibold text-slate-900 dark:text-slate-100">Two-factor authentication</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              {status?.enabled
                ? 'Enabled - your account requires a verification code at sign-in.'
                : 'Add an extra layer of security using an authenticator app.'}
            </p>
          </div>
        </div>
        {status?.enabled ? (
          <Button variant="danger" size="sm" onClick={() => setIsDisableOpen(true)}>
            Disable
          </Button>
        ) : (
          <Button size="sm" onClick={() => setIsSetupOpen(true)}>
            Enable
          </Button>
        )}
      </div>

      <Modal isOpen={isSetupOpen} onClose={closeSetup} title="Set up two-factor authentication">
        {setupStep === 'scan' ? (
          <div className="flex flex-col gap-4">
            {error && <Alert tone="error">{error}</Alert>}
            <p className="text-sm text-slate-600 dark:text-slate-300">
              Scan this QR code with an authenticator app (Google Authenticator, Authy, 1Password, ...), then enter the
              6-digit code it shows.
            </p>
            {qrDataUrl && (
              <img
                src={qrDataUrl}
                alt="Two-factor setup QR code"
                className="mx-auto h-48 w-48 rounded-lg border border-slate-200 dark:border-slate-700"
              />
            )}
            {setupMutation.data && (
              <div className="flex items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2 text-xs dark:bg-slate-800">
                <code className="truncate text-slate-600 dark:text-slate-300">{setupMutation.data.secret}</code>
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(setupMutation.data!.secret)
                    setCopied(true)
                    setTimeout(() => setCopied(false), 1500)
                  }}
                  aria-label={copied ? 'Secret copied' : 'Copy secret'}
                  className="shrink-0 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                >
                  {copied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
                </button>
              </div>
            )}
            <DigitCodeInput length={6} value={code} onChange={setCode} autoFocus />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={closeSetup}>
                Cancel
              </Button>
              <Button
                isLoading={enableMutation.isPending}
                disabled={code.length < 6}
                onClick={() => enableMutation.mutate()}
              >
                Confirm
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            <Alert tone="success">Two-factor authentication is now enabled.</Alert>
            <p className="text-sm text-slate-600 dark:text-slate-300">
              Save these recovery codes somewhere safe. Each one can be used once to sign in if you lose access to your
              authenticator app. They won&apos;t be shown again.
            </p>
            <div className="grid grid-cols-2 gap-2 rounded-lg bg-slate-50 p-3 font-mono text-sm dark:bg-slate-800">
              {recoveryCodes.map((c) => (
                <span key={c} className="text-slate-700 dark:text-slate-300">
                  {c}
                </span>
              ))}
            </div>
            <Button onClick={closeSetup}>I&apos;ve saved these codes</Button>
          </div>
        )}
      </Modal>

      <Modal
        isOpen={isDisableOpen}
        onClose={() => setIsDisableOpen(false)}
        title="Disable two-factor authentication"
        size="sm"
      >
        <div className="flex flex-col gap-4">
          {error && <Alert tone="error">{error}</Alert>}
          <p className="text-sm text-slate-600 dark:text-slate-300">Enter your password to confirm.</p>
          <Input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoFocus
          />
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setIsDisableOpen(false)}>
              Cancel
            </Button>
            <Button variant="danger" isLoading={disableMutation.isPending} onClick={() => disableMutation.mutate()}>
              Disable
            </Button>
          </div>
        </div>
      </Modal>
    </Card>
  )
}
