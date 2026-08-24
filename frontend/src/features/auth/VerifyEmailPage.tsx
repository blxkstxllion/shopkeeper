import { useSearchParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, CheckCircle2, XCircle, Store } from 'lucide-react'
import { verifyEmail } from '@/api/auth'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

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

// Public regardless of auth state - the link may be opened in a different browser/device than
// where the account was registered, and the backend endpoint itself needs no session either.
export function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const navigate = useNavigate()
  const { user } = useAuth()

  const { isLoading, error } = useQuery({
    queryKey: ['verify-email', token],
    queryFn: () => verifyEmail(token!),
    enabled: Boolean(token),
    retry: false,
  })

  const continueTo = () => navigate(user ? '/app' : '/login', { replace: true })

  if (!token) {
    return (
      <Shell>
        <Card className="p-6 text-center">
          <XCircle className="mx-auto mb-3 h-10 w-10 text-red-500" />
          <h2 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">
            Missing verification token
          </h2>
          <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
            This link is missing its token. Check the link in your email, or request a new one.
          </p>
          <Button className="w-full" onClick={continueTo}>
            Continue
          </Button>
        </Card>
      </Shell>
    )
  }

  if (isLoading) {
    return (
      <Shell>
        <Card className="p-6 text-center">
          <Loader2 className="mx-auto mb-3 h-10 w-10 animate-spin text-primary-600" />
          <h2 className="text-base font-semibold text-slate-900 dark:text-slate-100">Verifying your email…</h2>
        </Card>
      </Shell>
    )
  }

  if (error) {
    return (
      <Shell>
        <Card className="p-6 text-center">
          <XCircle className="mx-auto mb-3 h-10 w-10 text-red-500" />
          <h2 className="mb-2 text-base font-semibold text-slate-900 dark:text-slate-100">Verification failed</h2>
          <Alert tone="error">
            {error instanceof ApiError ? error.message : 'This link is invalid or has expired.'}
          </Alert>
          <Button className="mt-4 w-full" onClick={continueTo}>
            Continue
          </Button>
        </Card>
      </Shell>
    )
  }

  return (
    <Shell>
      <Card className="p-6 text-center">
        <CheckCircle2 className="mx-auto mb-3 h-10 w-10 text-good dark:text-good-dark" />
        <h2 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">Email verified</h2>
        <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">Your email address has been confirmed.</p>
        <Button className="w-full" onClick={continueTo}>
          Continue
        </Button>
      </Card>
    </Shell>
  )
}
