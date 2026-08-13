import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ShieldCheck, Store, Check } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { DigitCodeInput } from '@/components/ui/DigitCodeInput'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import type { User } from '@/types/auth'

const credentialsSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

type CredentialsFormValues = z.infer<typeof credentialsSchema>

const codeSchema = z.object({
  code: z.string().min(6, 'Enter the 6-digit code, or a recovery code').max(20),
})

type CodeFormValues = z.infer<typeof codeSchema>

const BRAND_HIGHLIGHTS = [
  'Real-time sales, inventory, and profit tracking',
  'Multi-branch support built in from day one',
  'Role-based access for your whole team',
]

export function LoginPage() {
  const { login, completeTwoFactorLogin } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const [challengeToken, setChallengeToken] = useState<string | null>(null)
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)

  const credentialsForm = useForm<CredentialsFormValues>({ resolver: zodResolver(credentialsSchema) })
  const codeForm = useForm<CodeFormValues>({ resolver: zodResolver(codeSchema) })
  const codeValue = codeForm.watch('code') ?? ''

  const goToNextScreen = (user: User) => {
    if (user.businesses.length === 0) {
      navigate('/onboarding', { replace: true })
    } else if (user.businesses.length === 1) {
      navigate('/app', { replace: true })
    } else {
      navigate('/select-business', { replace: true })
    }
  }

  const onSubmitCredentials = async (values: CredentialsFormValues) => {
    setServerError(null)
    try {
      const outcome = await login(values.email, values.password)
      if (outcome.requiresTwoFactor) {
        setChallengeToken(outcome.challengeToken)
      } else {
        goToNextScreen(outcome.user)
      }
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  const onSubmitCode = async (values: CodeFormValues) => {
    setServerError(null)
    try {
      const user = await completeTwoFactorLogin(challengeToken!, values.code)
      goToNextScreen(user)
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  return (
    <div className="flex min-h-screen">
      <div className="hidden w-1/2 flex-col justify-between bg-gradient-to-br from-primary-700 to-primary-900 p-12 text-white lg:flex">
        <div className="flex items-center gap-2">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/15">
            <Store className="h-5 w-5" />
          </div>
          <span className="text-lg font-semibold">The Shop Keeper</span>
        </div>

        <div>
          <h2 className="mb-6 text-3xl font-semibold leading-tight">Know your business. Grow your profit.</h2>
          <ul className="space-y-3">
            {BRAND_HIGHLIGHTS.map((highlight) => (
              <li key={highlight} className="flex items-center gap-2.5 text-sm text-primary-100">
                <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white/15">
                  <Check className="h-3 w-3" />
                </span>
                {highlight}
              </li>
            ))}
          </ul>
        </div>

        <p className="text-xs text-primary-200">&copy; {new Date().getFullYear()} The Shop Keeper</p>
      </div>

      <div className="flex w-full flex-col items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950 lg:w-1/2">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex flex-col items-center gap-2 text-center lg:hidden">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-600 text-white">
              <Store className="h-6 w-6" />
            </div>
            <h1 className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">Know Your Business. Grow Your Profit.</p>
          </div>

          {challengeToken ? (
            <Card className="p-6">
              <div className="mb-4 flex flex-col items-center gap-2 text-center">
                <ShieldCheck className="h-8 w-8 text-primary-600" />
                <h1 className="text-base font-semibold text-slate-900 dark:text-slate-100">Two-factor verification</h1>
                <p className="text-sm text-slate-500 dark:text-slate-400">
                  Enter the 6-digit code from your authenticator app, or one of your recovery codes.
                </p>
              </div>

              <form onSubmit={codeForm.handleSubmit(onSubmitCode)} className="flex flex-col gap-4">
                {serverError && <Alert tone="error">{serverError}</Alert>}

                {useRecoveryCode ? (
                  <FormField label="Recovery code" htmlFor="code" error={codeForm.formState.errors.code?.message}>
                    <Input
                      id="code"
                      autoFocus
                      autoComplete="one-time-code"
                      placeholder="Enter a recovery code"
                      {...codeForm.register('code')}
                      error={codeForm.formState.errors.code?.message}
                    />
                  </FormField>
                ) : (
                  <div className="flex flex-col items-center gap-1.5">
                    <DigitCodeInput
                      length={6}
                      value={codeValue}
                      onChange={(v) => codeForm.setValue('code', v, { shouldValidate: true })}
                      error={Boolean(codeForm.formState.errors.code)}
                      autoFocus
                    />
                    {codeForm.formState.errors.code && (
                      <p className="text-xs text-red-600 dark:text-red-400">{codeForm.formState.errors.code.message}</p>
                    )}
                  </div>
                )}

                <Button type="submit" isLoading={codeForm.formState.isSubmitting} className="w-full">
                  Verify and sign in
                </Button>
                <button
                  type="button"
                  onClick={() => {
                    setUseRecoveryCode((v) => !v)
                    codeForm.setValue('code', '')
                    codeForm.clearErrors('code')
                  }}
                  className="text-center text-sm font-medium text-primary-600 hover:text-primary-700"
                >
                  {useRecoveryCode ? 'Use an authenticator code instead' : 'Use a recovery code instead'}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setChallengeToken(null)
                    setServerError(null)
                  }}
                  className="text-center text-sm font-medium text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200"
                >
                  Back to sign in
                </button>
              </form>
            </Card>
          ) : (
            <Card className="p-6">
              <form onSubmit={credentialsForm.handleSubmit(onSubmitCredentials)} className="flex flex-col gap-4">
                {serverError && <Alert tone="error">{serverError}</Alert>}

                <FormField label="Email" htmlFor="email" error={credentialsForm.formState.errors.email?.message}>
                  <Input
                    id="email"
                    type="email"
                    autoComplete="email"
                    {...credentialsForm.register('email')}
                    error={credentialsForm.formState.errors.email?.message}
                  />
                </FormField>

                <FormField
                  label="Password"
                  htmlFor="password"
                  error={credentialsForm.formState.errors.password?.message}
                >
                  <Input
                    id="password"
                    type="password"
                    autoComplete="current-password"
                    {...credentialsForm.register('password')}
                    error={credentialsForm.formState.errors.password?.message}
                  />
                </FormField>

                <div className="-mt-2 flex justify-end">
                  <Link to="/forgot-password" className="text-sm font-medium text-primary-600 hover:text-primary-700">
                    Forgot password?
                  </Link>
                </div>

                <Button type="submit" isLoading={credentialsForm.formState.isSubmitting} className="w-full">
                  Sign in
                </Button>
              </form>

              <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
                Don&apos;t have an account?{' '}
                <Link to="/register" className="font-medium text-primary-600 hover:text-primary-700">
                  Create one
                </Link>
              </p>
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}
