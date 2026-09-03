import { useMutation } from '@tanstack/react-query'
import { Mail, LogOut } from 'lucide-react'
import { resendVerificationEmail } from '@/api/auth'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Logo } from '@/components/ui/Logo'
import { Button } from '@/components/ui/Button'

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

/** The hard block for accounts where verification is enforced (see RequireVerifiedEmail
 * guard) - distinct from EmailVerificationBanner, which is a dismissible nudge shown to
 * grandfathered pre-enforcement accounts that can still use the app unverified. */
export function VerificationRequiredPage() {
  const { user, logout, refreshUser } = useAuth()
  const resendMutation = useMutation({ mutationFn: resendVerificationEmail })

  return (
    <Shell>
      <Card className="p-6 text-center">
        <Mail className="mx-auto mb-3 h-10 w-10 text-primary-600" />
        <h2 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">Verify your email</h2>
        <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
          We sent a verification link to <span className="font-medium">{user?.email}</span>. Click it, then come back
          here.
        </p>

        {resendMutation.isSuccess ? (
          <p className="mb-3 text-sm font-medium text-good dark:text-good-dark">Verification email sent.</p>
        ) : (
          <Button
            variant="secondary"
            className="mb-3 w-full"
            onClick={() => resendMutation.mutate()}
            disabled={resendMutation.isPending}
          >
            {resendMutation.isPending ? 'Sending…' : 'Resend email'}
          </Button>
        )}

        <Button className="w-full" onClick={() => void refreshUser()}>
          I've verified — continue
        </Button>

        <button
          type="button"
          onClick={() => void logout()}
          className="mt-4 flex w-full items-center justify-center gap-1.5 text-sm text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
        >
          <LogOut className="h-3.5 w-3.5" />
          Sign out
        </button>
      </Card>
    </Shell>
  )
}
