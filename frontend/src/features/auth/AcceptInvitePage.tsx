import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { acceptInvitation, acceptInvitationForExistingUser, getInvitation } from '@/api/employees'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Logo } from '@/components/ui/Logo'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  password: z
    .string()
    .min(8, 'Must be at least 8 characters')
    .regex(/[A-Z]/, 'Must include an uppercase letter')
    .regex(/[a-z]/, 'Must include a lowercase letter')
    .regex(/[0-9]/, 'Must include a number'),
})

type FormValues = z.infer<typeof schema>

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <Logo className="h-11 w-11" />
          <h1 className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</h1>
        </div>
        {children}
      </div>
    </div>
  )
}

export function AcceptInvitePage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const navigate = useNavigate()
  const { user, applyAuthResult, logout } = useAuth()
  const [serverError, setServerError] = useState<string | null>(null)
  const [isAccepting, setIsAccepting] = useState(false)

  const {
    data: invitation,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['invitation', token],
    queryFn: () => getInvitation(token!),
    enabled: Boolean(token),
    retry: false,
  })

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  if (!token) {
    return (
      <Shell>
        <Card className="p-6">
          <Alert tone="error">This invite link is missing its token. Please ask for a new invitation.</Alert>
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

  if (error || !invitation) {
    return (
      <Shell>
        <Card className="p-6">
          <Alert tone="error">
            {error instanceof ApiError ? error.message : 'This invitation is invalid or has expired.'}
          </Alert>
        </Card>
      </Shell>
    )
  }

  const onSubmitNewUser = async (values: FormValues) => {
    setServerError(null)
    try {
      const result = await acceptInvitation(token, values)
      applyAuthResult(result, invitation.businessId)
      navigate('/app', { replace: true })
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    }
  }

  async function acceptAsExistingUser() {
    setServerError(null)
    setIsAccepting(true)
    try {
      const result = await acceptInvitationForExistingUser(token!)
      applyAuthResult(result, invitation!.businessId)
      navigate('/app', { replace: true })
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setIsAccepting(false)
    }
  }

  const header = (
    <div className="mb-4 text-center">
      <h2 className="text-base font-semibold text-slate-900 dark:text-slate-100">Join {invitation.businessName}</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
        {invitation.inviterName} invited you as {invitation.roleName}.
      </p>
    </div>
  )

  if (invitation.userAlreadyExists) {
    if (!user) {
      return (
        <Shell>
          <Card className="p-6">
            {header}
            {serverError && (
              <div className="mb-4">
                <Alert tone="error">{serverError}</Alert>
              </div>
            )}
            <p className="mb-4 text-center text-sm text-slate-500 dark:text-slate-400">
              You already have an account with {invitation.email}. Log in to accept this invitation.
            </p>
            <Button
              className="w-full"
              onClick={() => navigate(`/login?redirect=${encodeURIComponent(`/accept-invite?token=${token}`)}`)}
            >
              Log in to accept
            </Button>
          </Card>
        </Shell>
      )
    }

    if (user.email.toLowerCase() !== invitation.email.toLowerCase()) {
      return (
        <Shell>
          <Card className="p-6">
            {header}
            <Alert tone="error">
              {`This invitation was sent to ${invitation.email}, but you're signed in as ${user.email}.`}
            </Alert>
            <Button variant="secondary" className="mt-4 w-full" onClick={() => logout()}>
              Log out
            </Button>
          </Card>
        </Shell>
      )
    }

    return (
      <Shell>
        <Card className="p-6">
          {header}
          {serverError && (
            <div className="mb-4">
              <Alert tone="error">{serverError}</Alert>
            </div>
          )}
          <Button className="w-full" isLoading={isAccepting} onClick={acceptAsExistingUser}>
            Accept invitation
          </Button>
        </Card>
      </Shell>
    )
  }

  return (
    <Shell>
      <Card className="p-6">
        {header}
        <form onSubmit={handleSubmit(onSubmitNewUser)} className="flex flex-col gap-4">
          {serverError && <Alert tone="error">{serverError}</Alert>}

          <div className="grid grid-cols-2 gap-3">
            <FormField label="First name" htmlFor="firstName" error={errors.firstName?.message}>
              <Input
                id="firstName"
                autoComplete="given-name"
                {...register('firstName')}
                error={errors.firstName?.message}
              />
            </FormField>
            <FormField label="Last name" htmlFor="lastName" error={errors.lastName?.message}>
              <Input
                id="lastName"
                autoComplete="family-name"
                {...register('lastName')}
                error={errors.lastName?.message}
              />
            </FormField>
          </div>

          <FormField
            label="Password"
            htmlFor="password"
            error={errors.password?.message}
            hint="At least 8 characters, with an uppercase letter, lowercase letter, and number."
          >
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              {...register('password')}
              error={errors.password?.message}
            />
          </FormField>

          <Button type="submit" isLoading={isSubmitting} className="w-full">
            Create account &amp; join
          </Button>
        </form>
      </Card>
    </Shell>
  )
}
