import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, Sparkles } from 'lucide-react'
import { getPlanUsage, initiateCheckout, setInventoryAddOn, setPlanTier } from '@/api/plans'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { Skeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api-client'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { useOfflineSingletonQuery } from '@/offline/useOfflineQuery'
import type { PlanTier, PlanUsage } from '@/types/plans'

const TIERS: { id: PlanTier; label: string; price: string; blurb: string }[] = [
  { id: 'Free', label: 'Free', price: 'GH₵0/mo', blurb: 'Up to 2 branches, 25 products, 2 staff. No reports.' },
  {
    id: 'Business',
    label: 'Business',
    price: 'GH₵80/mo',
    blurb: 'Up to 5 branches, 250 products, 10 staff. Full reports.',
  },
  {
    id: 'BusinessAi',
    label: 'Business + AI',
    price: 'GH₵130/mo',
    blurb: 'Everything in Business, plus the AI Advisor.',
  },
  {
    id: 'Enterprise',
    label: 'Enterprise',
    price: 'GH₵250/mo',
    blurb: 'Unlimited branches & staff, up to 2,000 products. Full reports.',
  },
  {
    id: 'EnterpriseAi',
    label: 'Enterprise + AI',
    price: 'GH₵380/mo',
    blurb: 'Everything in Enterprise, plus the AI Advisor.',
  },
]

function UsageRow({ label, used, max }: { label: string; used: number; max: number }) {
  const unlimited = max >= 2_000_000_000 // int.MaxValue on the backend
  const percent = unlimited ? 0 : Math.min(100, (used / Math.max(max, 1)) * 100)
  return (
    <div>
      <div className="mb-1 flex justify-between text-xs text-slate-500 dark:text-slate-400">
        <span>{label}</span>
        <span>
          {used} of {unlimited ? 'unlimited' : max} used
        </span>
      </div>
      {!unlimited && (
        <div className="h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
          <div
            className={`h-full rounded-full ${percent >= 100 ? 'bg-red-500' : 'bg-primary-600'}`}
            style={{ width: `${percent}%` }}
          />
        </div>
      )}
    </div>
  )
}

export function PlanBillingSection() {
  const { activeBusiness } = useAuth()
  const queryClient = useQueryClient()
  const isOnline = useOnlineStatus()
  const [serverError, setServerError] = useState<string | null>(null)
  const isOwner = activeBusiness?.isOwner ?? false

  // Read-only offline (a cached GET) - changing plan/billing itself is payment-adjacent and
  // stays online-only, same boundary as Paystack checkout and the AI Advisor.
  const { data: usage, isLoading } = useOfflineSingletonQuery<PlanUsage>(['plan-usage'], 'planUsage', getPlanUsage)

  const tierMutation = useMutation({
    mutationFn: setPlanTier,
    onSuccess: () => {
      setServerError(null)
      queryClient.invalidateQueries({ queryKey: ['plan-usage'] })
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to change plan. Please try again.'),
  })

  const addOnMutation = useMutation({
    mutationFn: setInventoryAddOn,
    onSuccess: () => {
      setServerError(null)
      queryClient.invalidateQueries({ queryKey: ['plan-usage'] })
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to change plan. Please try again.'),
  })

  const checkoutMutation = useMutation({
    mutationFn: initiateCheckout,
    onSuccess: (session) => {
      // Full page navigation, not client-side routing - this leaves the SPA entirely for
      // Paystack's hosted checkout page.
      window.location.href = session.authorizationUrl
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to start checkout. Please try again.'),
  })

  if (isLoading || !usage) {
    return (
      <div className="flex flex-col gap-4">
        <Card className="p-4">
          <Skeleton className="mb-1 h-4 w-32" />
          <Skeleton className="mb-4 h-3 w-64" />
          <div className="flex flex-col gap-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i}>
                <div className="mb-1 flex justify-between">
                  <Skeleton className="h-3 w-16" />
                  <Skeleton className="h-3 w-20" />
                </div>
                <Skeleton className="h-1.5 w-full" />
              </div>
            ))}
          </div>
        </Card>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <Card key={i} className="p-4">
              <Skeleton className="mb-2 h-4 w-24" />
              <Skeleton className="mb-2 h-6 w-20" />
              <Skeleton className="mb-4 h-3 w-full" />
              <Skeleton className="h-8 w-full" />
            </Card>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <Card className="p-4">
        <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Plan &amp; billing</h2>
        <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
          {usage.billingEnabled
            ? 'Paid plans are billed monthly via Paystack. Downgrading to Free takes effect immediately.'
            : 'Self-serve for now - no card required yet. Changing your plan takes effect immediately.'}
          {!isOwner && ' Only the business owner can change plans.'}
          {usage.billingEnabled && usage.currentPeriodEnd && (
            <> Renews on {new Date(usage.currentPeriodEnd).toLocaleDateString()}.</>
          )}
        </p>

        {!isOnline && (
          <Alert tone="info">
            Your plan and usage shown here are from your last sync. Changing plans needs internet.
          </Alert>
        )}
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="mb-4 flex flex-col gap-3">
          <UsageRow label="Branches" used={usage.branchCount} max={usage.limits.maxBranches} />
          <UsageRow label="Products" used={usage.productCount} max={usage.limits.maxProducts} />
          <UsageRow label="Staff accounts" used={usage.staffCount} max={usage.limits.maxStaff} />
        </div>

        {isOwner && (
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
            <input
              type="checkbox"
              checked={usage.hasUnlimitedInventoryAddOn}
              disabled={addOnMutation.isPending || !isOnline}
              onChange={(e) => addOnMutation.mutate(e.target.checked)}
              className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
            />
            Unlimited inventory add-on (+GH₵60/mo)
          </label>
        )}
      </Card>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {TIERS.map((tier) => {
          const isCurrent = tier.id === usage.currentTier
          return (
            <Card key={tier.id} className={`p-4 ${isCurrent ? 'ring-2 ring-primary-500' : ''}`}>
              <div className="mb-1 flex items-center gap-1.5">
                <h3 className="text-sm font-semibold text-slate-900 dark:text-slate-100">{tier.label}</h3>
                {tier.id.endsWith('Ai') && <Sparkles className="h-3.5 w-3.5 text-primary-500" />}
              </div>
              <p className="mb-2 text-lg font-bold text-slate-900 dark:text-slate-100">{tier.price}</p>
              <p className="mb-4 text-xs text-slate-500 dark:text-slate-400">{tier.blurb}</p>
              {isCurrent ? (
                <div className="flex items-center gap-1.5 text-xs font-medium text-primary-700 dark:text-primary-400">
                  <Check className="h-3.5 w-3.5" />
                  Current plan
                </div>
              ) : isOwner ? (
                <Button
                  variant="secondary"
                  size="sm"
                  className="w-full"
                  isLoading={
                    (tierMutation.isPending && tierMutation.variables === tier.id) ||
                    (checkoutMutation.isPending && checkoutMutation.variables === tier.id)
                  }
                  disabled={tierMutation.isPending || checkoutMutation.isPending || !isOnline}
                  onClick={() => {
                    // Free never needs checkout, whether or not billing is enabled - and once
                    // billing is enabled, going through checkout for a paid tier is what keeps
                    // SetPlanTierCommand's server-side restriction (paid tiers require a
                    // completed purchase) satisfied instead of just erroring.
                    if (tier.id === 'Free' || !usage.billingEnabled) {
                      tierMutation.mutate(tier.id)
                    } else {
                      checkoutMutation.mutate(tier.id)
                    }
                  }}
                >
                  Switch to this plan
                </Button>
              ) : null}
            </Card>
          )
        })}
      </div>
    </div>
  )
}
