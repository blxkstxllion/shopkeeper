import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { createBranch, updateBranch } from '@/api/branches'
import { ApiError } from '@/lib/api-client'
import type { Branch } from '@/types/business'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  code: z.string().min(1, 'Code is required').max(20),
  address: z.string().optional().or(z.literal('')),
  city: z.string().optional().or(z.literal('')),
  country: z.string().optional().or(z.literal('')),
  phone: z.string().optional().or(z.literal('')),
  email: z.string().email('Enter a valid email address').optional().or(z.literal('')),
  isMain: z.boolean(),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = {
  name: '',
  code: '',
  address: '',
  city: '',
  country: '',
  phone: '',
  email: '',
  isMain: false,
}

export function BranchFormModal({
  isOpen,
  onClose,
  branch,
}: {
  isOpen: boolean
  onClose: () => void
  branch?: Branch | null
}) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const isEditing = Boolean(branch)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: branch
      ? {
          name: branch.name,
          code: branch.code,
          address: branch.address ?? '',
          city: branch.city ?? '',
          country: branch.country ?? '',
          phone: branch.phone ?? '',
          email: branch.email ?? '',
          isMain: branch.isMainBranch,
        }
      : defaults,
  })

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const payload = {
        name: values.name,
        code: values.code,
        address: values.address || null,
        city: values.city || null,
        country: values.country || null,
        phone: values.phone || null,
        email: values.email || null,
      }
      if (isEditing && branch) {
        await updateBranch({ ...payload, id: branch.id, isMain: values.isMain, isActive: branch.isActive })
      } else {
        await createBranch(payload)
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] })
      reset(defaults)
      onClose()
    },
    onError: (err) => {
      setServerError(err instanceof ApiError ? err.message : 'Unable to save this branch. Please try again.')
    },
  })

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit branch' : 'Add branch'} size="lg">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Name" htmlFor="name" error={errors.name?.message}>
            <Input id="name" {...register('name')} error={errors.name?.message} />
          </FormField>
          <FormField label="Code" htmlFor="code" error={errors.code?.message}>
            <Input id="code" {...register('code')} error={errors.code?.message} />
          </FormField>
        </div>

        <FormField label="Address (optional)" htmlFor="address">
          <Input id="address" {...register('address')} />
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="City (optional)" htmlFor="city">
            <Input id="city" {...register('city')} />
          </FormField>
          <FormField label="Country (optional)" htmlFor="country">
            <Input id="country" {...register('country')} />
          </FormField>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Phone (optional)" htmlFor="phone">
            <Input id="phone" {...register('phone')} />
          </FormField>
          <FormField label="Email (optional)" htmlFor="email" error={errors.email?.message}>
            <Input id="email" {...register('email')} error={errors.email?.message} />
          </FormField>
        </div>

        {isEditing && !branch?.isMainBranch && (
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
            <input
              type="checkbox"
              {...register('isMain')}
              className="h-4 w-4 rounded border-slate-300 text-primary-600"
            />
            Make this the main branch
          </label>
        )}

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending}>
            {isEditing ? 'Save changes' : 'Add branch'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
