import { useSearchParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, CheckCircle2, XCircle } from 'lucide-react'
import { verifyCheckout } from '@/api/plans'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { queryClient } from '@/lib/query-client'

// Paystack appends both ?reference= and ?trxref= to the callback URL - same value either way.
export function BillingCallbackPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const reference = searchParams.get('reference') ?? searchParams.get('trxref')

  const { data, isLoading, error } = useQuery({
    queryKey: ['verify-checkout', reference],
    queryFn: async () => {
      const result = await verifyCheckout(reference!)
      if (result.success) {
        await queryClient.invalidateQueries({ queryKey: ['plan-usage'] })
      }
      return result
    },
    enabled: Boolean(reference),
    retry: false,
  })

  return (
    <div className="mx-auto max-w-sm py-12">
      <Card className="p-6 text-center">
        {!reference ? (
          <>
            <XCircle className="mx-auto mb-3 h-10 w-10 text-red-500" />
            <h1 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">
              Missing checkout reference
            </h1>
            <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
              This link is missing its checkout reference. If you just completed a payment, contact support.
            </p>
          </>
        ) : isLoading ? (
          <>
            <Loader2 className="mx-auto mb-3 h-10 w-10 animate-spin text-primary-600" />
            <h1 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">Confirming payment…</h1>
            <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">This only takes a moment.</p>
          </>
        ) : error || !data?.success ? (
          <>
            <XCircle className="mx-auto mb-3 h-10 w-10 text-red-500" />
            <h1 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">Payment not confirmed</h1>
            <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
              We couldn&apos;t confirm this payment. If you were charged, contact support and we&apos;ll sort it out.
            </p>
          </>
        ) : (
          <>
            <CheckCircle2 className="mx-auto mb-3 h-10 w-10 text-good dark:text-good-dark" />
            <h1 className="mb-1 text-base font-semibold text-slate-900 dark:text-slate-100">Plan activated</h1>
            <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">Your {data.newTier} plan is now active.</p>
          </>
        )}
        <Button className="w-full" onClick={() => navigate('/app/settings?section=subscription')}>
          Back to Settings
        </Button>
      </Card>
    </div>
  )
}
