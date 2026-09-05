import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { Customer } from '@/types/customer'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  phone: z.string().optional().or(z.literal('')),
  email: z.string().email('Enter a valid email address').optional().or(z.literal('')),
  address: z.string().optional().or(z.literal('')),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = {
  name: '',
  phone: '',
  email: '',
  address: '',
}

export function CustomerFormModal({
  isOpen,
  onClose,
  customer,
}: {
  isOpen: boolean
  onClose: () => void
  customer?: Customer | null
}) {
  const [serverError, setServerError] = useState<string | null>(null)
  const isEditing = Boolean(customer)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: customer
      ? {
          name: customer.name,
          phone: customer.phone ?? '',
          email: customer.email ?? '',
          address: customer.address ?? '',
        }
      : defaults,
  })

  const createMutation = useOfflineMutation<{
    name: string
    phone: string | null
    email: string | null
    address: string | null
  }>('customer', (payload) => `New customer: ${payload.name}`)
  const updateMutation = useOfflineMutation<{
    name: string
    phone: string | null
    email: string | null
    address: string | null
    id: string
    isActive: boolean
  }>('customerUpdate', (payload) => `Update customer: ${payload.name}`)
  const isSaving = createMutation.isPending || updateMutation.isPending

  async function onSubmit(values: FormValues) {
    setServerError(null)
    const payload = {
      name: values.name,
      phone: values.phone || null,
      email: values.email || null,
      address: values.address || null,
    }
    try {
      if (isEditing && customer) {
        await updateMutation.mutateAsync({ payload: { ...payload, id: customer.id, isActive: customer.isActive } })
      } else {
        await createMutation.mutateAsync({ payload })
      }
      reset(defaults)
      onClose()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to save this customer. Please try again.')
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit customer' : 'Add customer'} size="lg">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="Name" htmlFor="name" error={errors.name?.message}>
          <Input id="name" {...register('name')} error={errors.name?.message} />
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Phone (optional)" htmlFor="phone">
            <Input id="phone" {...register('phone')} />
          </FormField>
          <FormField label="Email (optional)" htmlFor="email" error={errors.email?.message}>
            <Input id="email" {...register('email')} error={errors.email?.message} />
          </FormField>
        </div>

        <FormField label="Address (optional)" htmlFor="address">
          <Input id="address" {...register('address')} />
        </FormField>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || isSaving}>
            {isEditing ? 'Save changes' : 'Add customer'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
