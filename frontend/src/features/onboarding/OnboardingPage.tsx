import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Store, Check } from 'lucide-react'
import { clsx } from 'clsx'
import { useAuth } from '@/contexts/AuthContext'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import * as onboardingApi from '@/api/onboarding'
import type { BusinessGoal, BusinessType } from '@/types/business'
import {
  businessTypes,
  countries,
  currencies,
  goalOptions,
  onboardingDefaults,
  onboardingSchema,
  stepFields,
  type OnboardingFormValues,
} from './onboarding.schema'

const steps = ['Business', 'First branch', 'Tax settings', 'Goals', 'Review']

export function OnboardingPage() {
  const { completeOnboarding } = useAuth()
  const navigate = useNavigate()
  const [step, setStep] = useState(0)
  const [serverError, setServerError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const {
    register,
    handleSubmit,
    trigger,
    watch,
    setValue,
    formState: { errors },
  } = useForm<OnboardingFormValues>({
    resolver: zodResolver(onboardingSchema),
    defaultValues: onboardingDefaults,
    mode: 'onBlur',
  })

  const values = watch()

  const goNext = async () => {
    const fields = stepFields[step]
    const valid = await trigger(fields as (keyof OnboardingFormValues)[])
    if (valid) setStep((s) => Math.min(s + 1, steps.length - 1))
  }

  const goBack = () => setStep((s) => Math.max(s - 1, 0))

  const toggleGoal = (goal: string) => {
    const current = values.goals ?? []
    setValue('goals', current.includes(goal) ? current.filter((g) => g !== goal) : [...current, goal], {
      shouldValidate: true,
    })
  }

  const onSubmit = async (data: OnboardingFormValues) => {
    setServerError(null)
    setIsSubmitting(true)
    try {
      const result = await onboardingApi.completeOnboarding({
        businessName: data.businessName,
        businessType: data.businessType as BusinessType,
        country: data.country,
        currencyCode: data.currencyCode,
        taxEnabled: data.taxEnabled,
        taxRatePercent: data.taxRatePercent,
        taxInclusivePricing: data.taxInclusivePricing,
        goals: data.goals as BusinessGoal[],
        firstBranchName: data.firstBranchName,
        firstBranchAddress: data.firstBranchAddress || null,
        firstBranchCity: data.firstBranchCity || null,
      })

      completeOnboarding(result, result.user)
      navigate('/app', { replace: true })
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Something went wrong setting up your business.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 px-4 py-10 dark:bg-slate-950">
      <div className="mx-auto w-full max-w-xl">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-600 text-white">
            <Store className="h-6 w-6" />
          </div>
          <h1 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Set up your business</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">A few quick questions to get you started.</p>
        </div>

        <ol className="mb-6 flex items-center justify-center gap-2">
          {steps.map((label, i) => (
            <li key={label} className="flex items-center gap-2">
              <div
                className={clsx(
                  'flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold',
                  i < step && 'bg-primary-600 text-white',
                  i === step &&
                    'bg-primary-100 text-primary-700 ring-2 ring-primary-600 dark:bg-primary-900/40 dark:text-primary-300',
                  i > step && 'bg-slate-200 text-slate-500 dark:bg-slate-800 dark:text-slate-500',
                )}
              >
                {i < step ? <Check className="h-3.5 w-3.5" /> : i + 1}
              </div>
              {i < steps.length - 1 && <div className="h-px w-4 bg-slate-300 dark:bg-slate-700" />}
            </li>
          ))}
        </ol>

        <Card className="p-6">
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
            {serverError && <Alert tone="error">{serverError}</Alert>}

            {step === 0 && (
              <>
                <FormField label="Business name" htmlFor="businessName" error={errors.businessName?.message}>
                  <Input id="businessName" {...register('businessName')} error={errors.businessName?.message} />
                </FormField>
                <FormField label="Business type" htmlFor="businessType" error={errors.businessType?.message}>
                  <select
                    id="businessType"
                    {...register('businessType')}
                    className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                  >
                    {businessTypes.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                </FormField>
                <div className="grid grid-cols-2 gap-3">
                  <FormField label="Country" htmlFor="country" error={errors.country?.message}>
                    <select
                      id="country"
                      {...register('country')}
                      className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                    >
                      {countries.map((c) => (
                        <option key={c.value} value={c.value}>
                          {c.label}
                        </option>
                      ))}
                    </select>
                  </FormField>
                  <FormField label="Currency" htmlFor="currencyCode" error={errors.currencyCode?.message}>
                    <select
                      id="currencyCode"
                      {...register('currencyCode')}
                      className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                    >
                      {currencies.map((c) => (
                        <option key={c.value} value={c.value}>
                          {c.label}
                        </option>
                      ))}
                    </select>
                  </FormField>
                </div>
              </>
            )}

            {step === 1 && (
              <>
                <FormField
                  label="Branch name"
                  htmlFor="firstBranchName"
                  error={errors.firstBranchName?.message}
                  hint="e.g. Main Store, Accra Branch"
                >
                  <Input
                    id="firstBranchName"
                    {...register('firstBranchName')}
                    error={errors.firstBranchName?.message}
                  />
                </FormField>
                <FormField label="Address (optional)" htmlFor="firstBranchAddress">
                  <Input id="firstBranchAddress" {...register('firstBranchAddress')} />
                </FormField>
                <FormField label="City (optional)" htmlFor="firstBranchCity">
                  <Input id="firstBranchCity" {...register('firstBranchCity')} />
                </FormField>
              </>
            )}

            {step === 2 && (
              <>
                <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                  <input
                    type="checkbox"
                    {...register('taxEnabled')}
                    className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
                  />
                  Charge tax on sales
                </label>

                {values.taxEnabled && (
                  <>
                    <FormField label="Tax rate (%)" htmlFor="taxRatePercent" error={errors.taxRatePercent?.message}>
                      <Input
                        id="taxRatePercent"
                        type="number"
                        step="0.01"
                        {...register('taxRatePercent', { valueAsNumber: true })}
                        error={errors.taxRatePercent?.message}
                      />
                    </FormField>
                    <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                      <input
                        type="checkbox"
                        {...register('taxInclusivePricing')}
                        className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
                      />
                      Prices already include tax
                    </label>
                  </>
                )}
              </>
            )}

            {step === 3 && (
              <>
                <p className="text-sm text-slate-500 dark:text-slate-400">What matters most to you right now?</p>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {goalOptions.map((goal) => {
                    const checked = values.goals?.includes(goal.value)
                    return (
                      <button
                        type="button"
                        key={goal.value}
                        onClick={() => toggleGoal(goal.value)}
                        className={clsx(
                          'flex items-center gap-2 rounded-lg border px-3 py-2.5 text-left text-sm transition-colors',
                          checked
                            ? 'border-primary-500 bg-primary-50 text-primary-800 dark:bg-primary-900/30 dark:text-primary-300'
                            : 'border-slate-300 text-slate-700 hover:bg-slate-50 dark:border-slate-600 dark:text-slate-300 dark:hover:bg-slate-800',
                        )}
                      >
                        <div
                          className={clsx(
                            'flex h-4 w-4 shrink-0 items-center justify-center rounded border',
                            checked
                              ? 'border-primary-600 bg-primary-600 text-white'
                              : 'border-slate-300 dark:border-slate-600',
                          )}
                        >
                          {checked && <Check className="h-3 w-3" />}
                        </div>
                        {goal.label}
                      </button>
                    )
                  })}
                </div>
                {errors.goals && <p className="text-xs text-red-600 dark:text-red-400">{errors.goals.message}</p>}
              </>
            )}

            {step === 4 && (
              <div className="flex flex-col gap-3 text-sm">
                <SummaryRow label="Business" value={`${values.businessName} (${values.businessType})`} />
                <SummaryRow label="Location" value={`${values.country} · ${values.currencyCode}`} />
                <SummaryRow label="First branch" value={values.firstBranchName} />
                <SummaryRow label="Tax" value={values.taxEnabled ? `${values.taxRatePercent}%` : 'Not charging tax'} />
                <SummaryRow
                  label="Goals"
                  value={goalOptions
                    .filter((g: { value: string; label: string }) => values.goals?.includes(g.value))
                    .map((g: { value: string; label: string }) => g.label)
                    .join(', ')}
                />
              </div>
            )}

            <div className="mt-2 flex items-center justify-between">
              <Button type="button" variant="ghost" onClick={goBack} disabled={step === 0}>
                Back
              </Button>
              {step < steps.length - 1 ? (
                <Button type="button" onClick={goNext}>
                  Continue
                </Button>
              ) : (
                <Button type="submit" isLoading={isSubmitting}>
                  Finish setup
                </Button>
              )}
            </div>
          </form>
        </Card>
      </div>
    </div>
  )
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between border-b border-slate-100 pb-2 dark:border-slate-800">
      <span className="text-slate-500 dark:text-slate-400">{label}</span>
      <span className="font-medium text-slate-900 dark:text-slate-100">{value || '—'}</span>
    </div>
  )
}
