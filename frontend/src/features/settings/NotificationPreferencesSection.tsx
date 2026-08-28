import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { getNotificationPreferences, updateNotificationPreferences } from '@/api/notifications'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { Skeleton } from '@/components/ui/Skeleton'
import { ApiError } from '@/lib/api-client'
import type { NotificationPreferences } from '@/types/notification'

export function NotificationPreferencesSection() {
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['notification-preferences'], queryFn: getNotificationPreferences })
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)

  const { register, handleSubmit, reset } = useForm<NotificationPreferences>()

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: updateNotificationPreferences,
    onSuccess: () => {
      setServerError(null)
      setSuccessMessage('Notification preferences updated.')
      queryClient.invalidateQueries({ queryKey: ['notification-preferences'] })
      setTimeout(() => setSuccessMessage(null), 3000)
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to save changes. Please try again.'),
  })

  if (isLoading || !data) {
    return (
      <Card className="p-4">
        <div className="flex flex-col gap-4">
          {Array.from({ length: 2 }).map((_, i) => (
            <div key={i} className="flex items-start gap-2">
              <Skeleton className="mt-0.5 h-4 w-4 shrink-0 rounded" />
              <div className="flex-1">
                <Skeleton className="mb-1.5 h-3.5 w-28" />
                <Skeleton className="h-3 w-64" />
              </div>
            </div>
          ))}
          <div className="flex justify-end">
            <Skeleton className="h-9 w-32" />
          </div>
        </div>
      </Card>
    )
  }

  return (
    <Card className="p-4">
      <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Notifications</h2>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">
        Choose which events notify you. This only affects your own account - other owners set their preferences
        independently.
      </p>

      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {successMessage && <Alert tone="success">{successMessage}</Alert>}

        <label className="flex items-start gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            {...register('notifyOnJoinRequest')}
            className="mt-0.5 h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
          />
          <span>
            <span className="block">Join requests</span>
            <span className="block text-xs text-slate-400">Someone asks to join your business using a join code.</span>
          </span>
        </label>

        <label className="flex items-start gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            {...register('notifyOnLowStock')}
            className="mt-0.5 h-4 w-4 rounded border-slate-300 text-primary-600 focus:ring-primary-500"
          />
          <span>
            <span className="block">Low stock alerts</span>
            <span className="block text-xs text-slate-400">
              A product's quantity on hand drops to or below its reorder level.
            </span>
          </span>
        </label>

        <div className="flex justify-end">
          <Button type="submit" isLoading={mutation.isPending}>
            Save changes
          </Button>
        </div>
      </form>
    </Card>
  )
}
