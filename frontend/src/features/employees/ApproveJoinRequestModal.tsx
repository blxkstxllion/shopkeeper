import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { getRoles } from '@/api/roles'
import { getBranches } from '@/api/branches'
import { ApiError } from '@/lib/api-client'
import { useOfflineListQuery } from '@/offline/useOfflineQuery'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { JoinRequestItem, Role } from '@/types/employee'
import type { Branch } from '@/types/business'

const schema = z.object({
  roleId: z.string().min(1, 'Choose a role'),
  branchId: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

export function ApproveJoinRequestModal({
  isOpen,
  onClose,
  request,
}: {
  isOpen: boolean
  onClose: () => void
  request: JoinRequestItem | null
}) {
  const [serverError, setServerError] = useState<string | null>(null)

  const { data: roles } = useOfflineListQuery<Role>(['roles'], 'roles', getRoles, isOpen)
  const { data: branches } = useOfflineListQuery<Branch>(['branches'], 'branches', getBranches, isOpen)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { roleId: '', branchId: '' } })

  const mutation = useOfflineMutation<{ id: string; roleId: string; branchId: string | null }>(
    'joinRequestApprove',
    () => `Approve join request`,
  )

  async function onSubmit(values: FormValues) {
    if (!request) return
    setServerError(null)
    try {
      await mutation.mutateAsync({
        payload: { id: request.id, roleId: values.roleId, branchId: values.branchId || null },
      })
      reset()
      onClose()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to approve this request. Please try again.')
    }
  }

  function handleClose() {
    setServerError(null)
    onClose()
  }

  if (!request) return null

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={`Approve ${request.firstName} ${request.lastName}`} size="sm">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="Role" htmlFor="roleId" error={errors.roleId?.message}>
          <select
            id="roleId"
            {...register('roleId')}
            className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">Select a role</option>
            {roles?.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </FormField>

        <FormField label="Branch (optional)" htmlFor="branchId">
          <select
            id="branchId"
            {...register('branchId')}
            className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">All branches</option>
            {branches?.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </FormField>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={handleClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending}>
            Approve
          </Button>
        </div>
      </form>
    </Modal>
  )
}
