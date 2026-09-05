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
import type { Supplier } from '@/types/supplier'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  contactName: z.string().optional().or(z.literal('')),
  phone: z.string().optional().or(z.literal('')),
  email: z.string().email('Enter a valid email address').optional().or(z.literal('')),
  address: z.string().optional().or(z.literal('')),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = {
  name: '',
  contactName: '',
  phone: '',
  email: '',
  address: '',
}

export function SupplierFormModal({
  isOpen,
  onClose,
  supplier,
}: {
  isOpen: boolean
  onClose: () => void
  supplier?: Supplier | null
}) {
  const [serverError, setServerError] = useState<string | null>(null)
  const isEditing = Boolean(supplier)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: supplier
      ? {
          name: supplier.name,
          contactName: supplier.contactName ?? '',
          phone: supplier.phone ?? '',
          email: supplier.email ?? '',
          address: supplier.address ?? '',
        }
      : defaults,
  })

  const createMutation = useOfflineMutation<{
    name: string
    contactName: string | null
    phone: string | null
    email: string | null
    address: string | null
  }>('supplier', (payload) => `New supplier: ${payload.name}`)
  const updateMutation = useOfflineMutation<{
    name: string
    contactName: string | null
    phone: string | null
    email: string | null
    address: string | null
    id: string
    isActive: boolean
  }>('supplierUpdate', (payload) => `Update supplier: ${payload.name}`)
  const isSaving = createMutation.isPending || updateMutation.isPending

  async function onSubmit(values: FormValues) {
    setServerError(null)
    const payload = {
      name: values.name,
      contactName: values.contactName || null,
      phone: values.phone || null,
      email: values.email || null,
      address: values.address || null,
    }
    try {
      if (isEditing && supplier) {
        await updateMutation.mutateAsync({ payload: { ...payload, id: supplier.id, isActive: supplier.isActive } })
      } else {
        await createMutation.mutateAsync({ payload })
      }
      reset(defaults)
      onClose()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to save this supplier. Please try again.')
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit supplier' : 'Add supplier'} size="lg">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Name" htmlFor="name" error={errors.name?.message}>
            <Input id="name" {...register('name')} error={errors.name?.message} />
          </FormField>
          <FormField label="Contact name (optional)" htmlFor="contactName">
            <Input id="contactName" {...register('contactName')} />
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

        <FormField label="Address (optional)" htmlFor="address">
          <Input id="address" {...register('address')} />
        </FormField>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || isSaving}>
            {isEditing ? 'Save changes' : 'Add supplier'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
