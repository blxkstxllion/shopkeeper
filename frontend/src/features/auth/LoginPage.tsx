import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ShieldCheck } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
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

export function LoginPage() {
  const { login, completeTwoFactorLogin } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const [challengeToken, setChallengeToken] = useState<string | null>(null)

  const credentialsForm = useForm<CredentialsFormValues>({ resolver: zodResolver(credentialsSchema) })
  const codeForm = useForm<CodeFormValues>({ resolver: zodResolver(codeSchema) })

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

  if (challengeToken) {
    return (
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

          <FormField label="Verification code" htmlFor="code" error={codeForm.formState.errors.code?.message}>
            <Input
              id="code"
              autoFocus
              autoComplete="one-time-code"
              placeholder="123456"
              {...codeForm.register('code')}
              error={codeForm.formState.errors.code?.message}
            />
          </FormField>

          <Button type="submit" isLoading={codeForm.formState.isSubmitting} className="w-full">
            Verify and sign in
          </Button>
          <button
            type="button"
            onClick={() => {
              setChallengeToken(null)
              setServerError(null)
            }}
            className="text-center text-sm font-medium text-primary-600 hover:text-primary-700"
          >
            Back to sign in
          </button>
        </form>
      </Card>
    )
  }

  return (
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

        <FormField label="Password" htmlFor="password" error={credentialsForm.formState.errors.password?.message}>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            {...credentialsForm.register('password')}
            error={credentialsForm.formState.errors.password?.message}
          />
        </FormField>

        <div className="flex justify-end -mt-2">
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
  )
}
