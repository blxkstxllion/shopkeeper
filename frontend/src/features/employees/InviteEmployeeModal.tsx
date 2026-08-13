import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { inviteEmployee } from '@/api/employees'
import { getRoles } from '@/api/roles'
import { getBranches } from '@/api/branches'
import type { ApiErrorPayload } from '@/types/auth'

const schema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email address'),
  roleId: z.string().min(1, 'Choose a role'),
  branchId: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = { email: '', roleId: '', branchId: '' }

export function InviteEmployeeModal({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)

  const { data: roles } = useQuery({ queryKey: ['roles'], queryFn: getRoles, enabled: isOpen })
  const { data: branches } = useQuery({ queryKey: ['branches'], queryFn: getBranches, enabled: isOpen })

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults })

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      inviteEmployee({ email: values.email, roleId: values.roleId, branchId: values.branchId || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['business-users'] })
      reset(defaults)
      onClose()
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to send this invitation. Please try again.')
    },
  })

  function handleClose() {
    setServerError(null)
    onClose()
  }

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Invite a team member" size="lg">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="Email" htmlFor="email" error={errors.email?.message}>
          <Input id="email" type="email" {...register('email')} error={errors.email?.message} />
        </FormField>

        <div className="grid grid-cols-2 gap-3">
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
        </div>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={handleClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending}>
            Send invitation
          </Button>
        </div>
      </form>
    </Modal>
  )
}
