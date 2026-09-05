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
import { applyColorTheme } from '@/lib/colorTheme'
import { useOfflineSingletonQuery } from '@/offline/useOfflineQuery'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { BusinessSettings } from '@/types/businessSettings'

const schema = z.object({
  name: z.string().min(1, 'Business name is required').max(200),
  legalName: z.string().max(200).optional().or(z.literal('')),
  timeZone: z.string().min(1, 'Time zone is required').max(100),
  colorTheme: z.enum(['blue', 'red', 'green']),
})

type FormValues = z.infer<typeof schema>

const COLOR_THEME_OPTIONS: { value: FormValues['colorTheme']; label: string; swatchClass: string }[] = [
  { value: 'blue', label: 'Blue', swatchClass: 'bg-blue-600' },
  { value: 'red', label: 'Red', swatchClass: 'bg-red-600' },
  { value: 'green', label: 'Green', swatchClass: 'bg-emerald-600' },
]

export function BusinessProfileSection() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useOfflineSingletonQuery<BusinessSettings>(
    ['business-settings'],
    'businessSettings',
    getBusinessSettings,
  )
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, reset, watch, setValue, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
  })

  useEffect(() => {
    if (data) {
      reset({
        name: data.name,
        legalName: data.legalName ?? '',
        timeZone: data.timeZone,
        colorTheme: data.colorTheme as FormValues['colorTheme'],
      })
    }
  }, [data, reset])

  const colorTheme = watch('colorTheme')

  const mutation = useOfflineMutation<{ name: string; legalName: string | null; timeZone: string; colorTheme: string }>(
    'businessProfile',
    () => 'Update business profile',
  )

  async function onSubmit(values: FormValues) {
    setServerError(null)
    try {
      await mutation.mutateAsync({ payload: { ...values, legalName: values.legalName || null } })
      setSuccessMessage('Business profile updated.')
      // AuthContext only re-applies the color theme when activeBusiness changes (login/business
      // switch) - a Settings save doesn't refetch that snapshot, so apply it directly here too,
      // otherwise "saved" would be true but the page wouldn't visibly reflect it until next login.
      applyColorTheme(values.colorTheme)
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
        <Alert tone="error">You don&apos;t have permission to view business settings.</Alert>
      </Card>
    )
  }

  return (
    <Card className="p-4">
      <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Business profile</h2>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Business type, country, and currency were set during onboarding and can&apos;t be changed here since past
        transactions already depend on them.
      </p>

      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {successMessage && <Alert tone="success">{successMessage}</Alert>}

        <FormField label="Business name" htmlFor="name" error={formState.errors.name?.message}>
          <Input id="name" {...register('name')} error={formState.errors.name?.message} />
        </FormField>

        <FormField label="Legal name" htmlFor="legalName" error={formState.errors.legalName?.message} hint="Optional">
          <Input id="legalName" {...register('legalName')} error={formState.errors.legalName?.message} />
        </FormField>

        <FormField label="Time zone" htmlFor="timeZone" error={formState.errors.timeZone?.message}>
          <Input id="timeZone" {...register('timeZone')} error={formState.errors.timeZone?.message} />
        </FormField>

        <div>
          <p className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-300">Brand color</p>
          <div className="flex gap-3" role="radiogroup" aria-label="Brand color">
            {COLOR_THEME_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={colorTheme === option.value}
                onClick={() => setValue('colorTheme', option.value, { shouldDirty: true })}
                className={`flex flex-col items-center gap-1.5 rounded-lg border-2 px-3 py-2 text-xs font-medium transition-colors ${
                  colorTheme === option.value
                    ? 'border-primary-600 text-slate-900 dark:text-slate-100'
                    : 'border-transparent text-slate-500 hover:border-slate-200 dark:text-slate-400 dark:hover:border-slate-700'
                }`}
              >
                <span className={`h-6 w-6 rounded-full ${option.swatchClass}`} />
                {option.label}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4 rounded-lg bg-slate-50 p-3 text-sm dark:bg-slate-800">
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-400">Business type</p>
            <p className="text-slate-700 dark:text-slate-200">
              {data.businessType === 'Other' && data.businessTypeOther ? data.businessTypeOther : data.businessType}
            </p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-400">Country</p>
            <p className="text-slate-700 dark:text-slate-200">{data.country}</p>
          </div>
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-400">Currency</p>
            <p className="text-slate-700 dark:text-slate-200">{data.currencyCode}</p>
          </div>
        </div>

        <div className="flex justify-end">
          <Button type="submit" isLoading={mutation.isPending}>
            Save changes
          </Button>
        </div>
      </form>
    </Card>
  )
}
