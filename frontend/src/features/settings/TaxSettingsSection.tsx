import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { getBusinessSettings } from '@/api/businessSettings'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { FormSkeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api-client'
import { useOfflineSingletonQuery } from '@/offline/useOfflineQuery'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { BusinessSettings } from '@/types/businessSettings'

const schema = z.object({
  taxEnabled: z.boolean(),
  taxIdNumber: z.string().max(100).optional().or(z.literal('')),
  taxRatePercent: z.number().min(0).max(100),
  taxInclusivePricing: z.boolean(),
})

type FormValues = z.infer<typeof schema>

export function TaxSettingsSection() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useOfflineSingletonQuery<BusinessSettings>(
    ['business-settings'],
    'businessSettings',
    getBusinessSettings,
  )
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, reset, watch, formState } = useForm<FormValues>({ resolver: zodResolver(schema) })
  const taxEnabled = watch('taxEnabled')

  useEffect(() => {
    if (data) {
      reset({
        taxEnabled: data.taxEnabled,
        taxIdNumber: data.taxIdNumber ?? '',
        taxRatePercent: data.taxRatePercent,
        taxInclusivePricing: data.taxInclusivePricing,
      })
    }
  }, [data, reset])

  const mutation = useOfflineMutation<{
    taxEnabled: boolean
    taxIdNumber: string | null
    taxRatePercent: number
    taxInclusivePricing: boolean
  }>('taxSettings', () => 'Update tax settings')

  async function onSubmit(values: FormValues) {
    setServerError(null)
    try {
      await mutation.mutateAsync({ payload: { ...values, taxIdNumber: values.taxIdNumber || null } })
      setSuccessMessage('Tax settings updated.')
      await queryClient.invalidateQueries({ queryKey: ['business-settings'] })
      setTimeout(() => setSuccessMessage(null), 3000)
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to save changes. Please try again.')
    }
  }

  if (isLoading) {
    return (
      <Card className="p-4">
        <FormSkeleton fields={3} />
      </Card>
    )
  }

  if (isError || !data) {
    return (
      <Card className="p-4">
        <Alert tone="error">You don&apos;t have permission to view tax settings.</Alert>
      </Card>
    )
  }

  return (
    <Card className="p-4">
      <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Tax &amp; currency</h2>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        All amounts are recorded in{' '}
        <span className="font-medium text-slate-700 dark:text-slate-300">{data.currencyCode}</span>, set during
        onboarding.
      </p>

      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {successMessage && <Alert tone="success">{successMessage}</Alert>}

        <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            {...register('taxEnabled')}
            className="h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
          />
          Charge tax on sales
        </label>

        {taxEnabled && (
          <>
            <FormField
              label="Tax ID number"
              htmlFor="taxIdNumber"
              error={formState.errors.taxIdNumber?.message}
              hint="Optional"
            >
              <Input id="taxIdNumber" {...register('taxIdNumber')} error={formState.errors.taxIdNumber?.message} />
            </FormField>

            <FormField label="Tax rate (%)" htmlFor="taxRatePercent" error={formState.errors.taxRatePercent?.message}>
              <Input
                id="taxRatePercent"
                type="number"
                step="0.01"
                {...register('taxRatePercent', { valueAsNumber: true })}
                error={formState.errors.taxRatePercent?.message}
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

        <div className="flex justify-end">
          <Button type="submit" isLoading={mutation.isPending}>
            Save changes
          </Button>
        </div>
      </form>
    </Card>
  )
}
