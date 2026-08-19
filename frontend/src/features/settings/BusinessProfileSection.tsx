import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { getBusinessSettings, updateBusinessProfile } from '@/api/businessSettings'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { FormSkeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api-client'

const schema = z.object({
  name: z.string().min(1, 'Business name is required').max(200),
  legalName: z.string().max(200).optional().or(z.literal('')),
  timeZone: z.string().min(1, 'Time zone is required').max(100),
})

type FormValues = z.infer<typeof schema>

export function BusinessProfileSection() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError } = useQuery({ queryKey: ['business-settings'], queryFn: getBusinessSettings })
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, reset, formState } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (data) {
      reset({ name: data.name, legalName: data.legalName ?? '', timeZone: data.timeZone })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: FormValues) => updateBusinessProfile({ ...values, legalName: values.legalName || null }),
    onSuccess: () => {
      setServerError(null)
      setSuccessMessage('Business profile updated.')
      queryClient.invalidateQueries({ queryKey: ['business-settings'] })
      setTimeout(() => setSuccessMessage(null), 3000)
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to save changes. Please try again.'),
  })

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

      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
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

        <div className="grid grid-cols-2 gap-4 rounded-lg bg-slate-50 p-3 text-sm dark:bg-slate-800">
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-400">Business type</p>
            <p className="text-slate-700 dark:text-slate-200">{data.businessType}</p>
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
