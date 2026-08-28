import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Loader2, Store } from 'lucide-react'
import { getBusinessByCode, submitJoinRequest, submitJoinRequestForExistingUser } from '@/api/join'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

const codeSchema = z.object({ code: z.string().min(1, 'Enter a join code') })
type CodeFormValues = z.infer<typeof codeSchema>

const requestSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  phone: z.string().min(1, 'Phone number is required'),
  password: z
    .string()
    .min(8, 'Must be at least 8 characters')
    .regex(/[A-Z]/, 'Must include an uppercase letter')
    .regex(/[a-z]/, 'Must include a lowercase letter')
    .regex(/[0-9]/, 'Must include a number'),
})
type RequestFormValues = z.infer<typeof requestSchema>

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-600 text-white">
            <Store className="h-6 w-6" />
          </div>
          <h1 className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</h1>
        </div>
        {children}
      </div>
    </div>
  )
}

export function JoinPage() {
  const [searchParams] = useSearchParams()
  const code = searchParams.get('code')
  const navigate = useNavigate()
  const { user } = useAuth()
  const [serverError, setServerError] = useState<string | null>(null)
  const [submitted, setSubmitted] = useState(false)

  const codeForm = useForm<CodeFormValues>({ resolver: zodResolver(codeSchema) })
  const requestForm = useForm<RequestFormValues>({ resolver: zodResolver(requestSchema) })

  const {
    data: business,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['join-business', code],
    queryFn: () => getBusinessByCode(code!),
    enabled: Boolean(code),
    retry: false,
  })

  const requestMutation = useMutation({
    mutationFn: (values: RequestFormValues) => submitJoinRequest(code!, values),
    onSuccess: () => setSubmitted(true),
    onError: (err) => {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    },
  })

  const existingUserMutation = useMutation({
    mutationFn: () => submitJoinRequestForExistingUser(code!),
    onSuccess: () => setSubmitted(true),
    onError: (err) => {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    },
  })

  if (!code) {
    return (
      <Shell>
        <Card className="p-6">
          <p className="mb-4 text-center text-sm text-slate-500 dark:text-slate-400">
            Enter the join code your employer gave you.
          </p>
          <form
            onSubmit={codeForm.handleSubmit((values) => navigate(`/join?code=${values.code.trim()}`))}
            className="flex flex-col gap-4"
          >
            <FormField label="Join code" htmlFor="code" error={codeForm.formState.errors.code?.message}>
              <Input
                id="code"
                autoFocus
                className="text-center text-lg tracking-widest"
                {...codeForm.register('code')}
                error={codeForm.formState.errors.code?.message}
              />
            </FormField>
            <Button type="submit" className="w-full">
              Continue
            </Button>
          </form>
        </Card>
      </Shell>
    )
  }

  if (isLoading) {
    return (
      <Shell>
        <div className="flex justify-center">
          <Loader2 className="h-6 w-6 animate-spin text-primary-600" />
        </div>
      </Shell>
    )
  }

  if (error || !business) {
    return (
      <Shell>
        <Card className="p-6">
          <Alert tone="error">
            {error instanceof ApiError ? error.message : 'This join code is invalid or has been revoked.'}
          </Alert>
        </Card>
      </Shell>
    )
  }

  const header = (
    <div className="mb-4 text-center">
      <h2 className="text-base font-semibold text-slate-900 dark:text-slate-100">Join {business.businessName}</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
        Your request will need to be approved before you can sign in.
      </p>
    </div>
  )

  if (submitted) {
    return (
      <Shell>
        <Card className="p-6">
          {header}
          <Alert tone="success">
            Your request has been sent. You&apos;ll be able to sign in once the owner approves it.
          </Alert>
        </Card>
      </Shell>
    )
  }

  if (user) {
    return (
      <Shell>
        <Card className="p-6">
          {header}
          {serverError && (
            <div className="mb-4">
              <Alert tone="error">{serverError}</Alert>
            </div>
          )}
          <Button
            className="w-full"
            isLoading={existingUserMutation.isPending}
            onClick={() => existingUserMutation.mutate()}
          >
            Request to join
          </Button>
        </Card>
      </Shell>
    )
  }

  return (
    <Shell>
      <Card className="p-6">
        {header}
        <form
          onSubmit={requestForm.handleSubmit((values) => requestMutation.mutate(values))}
          className="flex flex-col gap-4"
        >
          {serverError && (
            <div className="flex flex-col gap-2">
              <Alert tone="error">{serverError}</Alert>
              {serverError.toLowerCase().includes('already exists') && (
                <Button
                  type="button"
                  variant="secondary"
                  className="w-full"
                  onClick={() => navigate(`/login?redirect=${encodeURIComponent(`/join?code=${code}`)}`)}
                >
                  Log in instead
                </Button>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <FormField label="First name" htmlFor="firstName" error={requestForm.formState.errors.firstName?.message}>
              <Input
                id="firstName"
                autoComplete="given-name"
                {...requestForm.register('firstName')}
                error={requestForm.formState.errors.firstName?.message}
              />
            </FormField>
            <FormField label="Last name" htmlFor="lastName" error={requestForm.formState.errors.lastName?.message}>
              <Input
                id="lastName"
                autoComplete="family-name"
                {...requestForm.register('lastName')}
                error={requestForm.formState.errors.lastName?.message}
              />
            </FormField>
          </div>

          <FormField label="Phone" htmlFor="phone" error={requestForm.formState.errors.phone?.message}>
            <Input
              id="phone"
              autoComplete="tel"
              {...requestForm.register('phone')}
              error={requestForm.formState.errors.phone?.message}
            />
          </FormField>

          <FormField label="Email" htmlFor="email" error={requestForm.formState.errors.email?.message}>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              {...requestForm.register('email')}
              error={requestForm.formState.errors.email?.message}
            />
          </FormField>

          <FormField
            label="Password"
            htmlFor="password"
            error={requestForm.formState.errors.password?.message}
            hint="At least 8 characters, with an uppercase letter, lowercase letter, and number."
          >
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              {...requestForm.register('password')}
              error={requestForm.formState.errors.password?.message}
            />
          </FormField>

          <Button type="submit" isLoading={requestMutation.isPending} className="w-full">
            Request to join
          </Button>
        </form>
      </Card>
    </Shell>
  )
}
