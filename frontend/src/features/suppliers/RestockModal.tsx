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
import { getProducts } from '@/api/products'
import { restockFromSupplier } from '@/api/suppliers'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import type { ApiErrorPayload } from '@/types/auth'
import type { Supplier } from '@/types/supplier'

const schema = z.object({
  productId: z.string().min(1, 'Select a product'),
  quantity: z.number().int().positive('Must be greater than 0'),
  unitCost: z.number().min(0).optional(),
})

type FormValues = z.infer<typeof schema>

export function RestockModal({
  isOpen,
  onClose,
  supplier,
}: {
  isOpen: boolean
  onClose: () => void
  supplier: Supplier | null
}) {
  const queryClient = useQueryClient()
  const { branch } = useActiveBranch()
  const [serverError, setServerError] = useState<string | null>(null)

  const { data } = useQuery({
    queryKey: ['products', { activeOnly: true, pageSize: 200 }],
    queryFn: () => getProducts({ activeOnly: true, pageSize: 200 }),
    enabled: isOpen,
  })
  const trackedProducts = (data?.items ?? []).filter((p) => p.trackInventory)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { productId: '', quantity: 0 } })

  const mutation = useMutation({
    mutationFn: (values: FormValues) => {
      if (!supplier || !branch) throw new Error('Missing supplier or branch')
      return restockFromSupplier(supplier.id, {
        productId: values.productId,
        branchId: branch.id,
        quantity: values.quantity,
        unitCost: values.unitCost ?? null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      queryClient.invalidateQueries({ queryKey: ['supplier-restocks', supplier?.id] })
      reset()
      onClose()
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to record this restock. Please try again.')
    },
  })

  if (!supplier) return null

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={`Restock from ${supplier.name}`} size="sm">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {!branch && <Alert tone="error">Select a branch before recording a restock.</Alert>}

        <FormField label="Product" htmlFor="productId" error={errors.productId?.message}>
          <select
            id="productId"
            {...register('productId')}
            className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">Select a product</option>
            {trackedProducts.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.sku})
              </option>
            ))}
          </select>
        </FormField>

        <FormField label="Quantity received" htmlFor="quantity" error={errors.quantity?.message}>
          <Input
            id="quantity"
            type="number"
            {...register('quantity', { valueAsNumber: true })}
            error={errors.quantity?.message}
          />
        </FormField>

        <FormField
          label="Unit cost (optional)"
          htmlFor="unitCost"
          hint="Updates the product's cost price if it's changed."
        >
          <Input id="unitCost" type="number" step="0.01" {...register('unitCost', { valueAsNumber: true })} />
        </FormField>

        <div className="mt-1 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending} disabled={!branch}>
            Record restock
          </Button>
        </div>
      </form>
    </Modal>
  )
}
