import { useState } from 'react'
import { Mail, X } from 'lucide-react'
import { useMutation } from '@tanstack/react-query'
import { resendVerificationEmail } from '@/api/auth'
import { useAuth } from '@/contexts/AuthContext'

export function EmailVerificationBanner() {
  const { user } = useAuth()
  const [dismissed, setDismissed] = useState(false)

  const resendMutation = useMutation({ mutationFn: resendVerificationEmail })

  if (!user || user.isEmailVerified || dismissed) return null

  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-900/20 dark:text-amber-300">
      <Mail className="h-4 w-4 shrink-0" />
      <span className="flex-1">Please verify your email address ({user.email}) to keep your account secure.</span>
      {resendMutation.isSuccess ? (
        <span className="font-medium">Verification email sent.</span>
      ) : (
        <button
          type="button"
          onClick={() => resendMutation.mutate()}
          disabled={resendMutation.isPending}
          className="font-medium underline hover:no-underline disabled:opacity-60"
        >
          {resendMutation.isPending ? 'Sending…' : 'Resend email'}
        </button>
      )}
      <button
        type="button"
        onClick={() => setDismissed(true)}
        aria-label="Dismiss"
        className="ml-1 shrink-0 text-amber-500 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-200"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}
