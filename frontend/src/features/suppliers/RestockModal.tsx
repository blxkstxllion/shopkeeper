import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { getProducts } from '@/api/products'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { ApiError } from '@/lib/api-client'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import { useOfflineSingletonQuery } from '@/offline/useOfflineQuery'
import type { PagedResult, Product } from '@/types/product'
import type { RestockFromSupplierPayload } from '@/types/supplier'
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

  // Singleton, not the branch-indexed products store (see cache.ts) - this dropdown deliberately
  // lists every active product across the whole business, unfiltered by branch, unlike
  // InventoryPage's per-branch view.
  const { data } = useOfflineSingletonQuery<PagedResult<Product>>(
    ['products', 'restock-dropdown'],
    'products:restockDropdown',
    () => getProducts({ activeOnly: true, pageSize: 200 }),
    isOpen,
  )
  const trackedProducts = (data?.items ?? []).filter((p) => p.trackInventory)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { productId: '', quantity: 0 } })

  const mutation = useOfflineMutation<{ supplierId: string } & RestockFromSupplierPayload>(
    'restock',
    (payload) => `Restock: ${payload.quantity} units`,
  )

  async function onSubmit(values: FormValues) {
    if (!supplier || !branch) return
    setServerError(null)
    try {
      await mutation.mutateAsync({
        payload: {
          supplierId: supplier.id,
          productId: values.productId,
          branchId: branch.id,
          quantity: values.quantity,
          unitCost: values.unitCost ?? null,
        },
      })
      queryClient.invalidateQueries({ queryKey: ['supplier-restocks', supplier.id] })
      reset()
      onClose()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to record this restock. Please try again.')
    }
  }

  if (!supplier) return null

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={`Restock from ${supplier.name}`} size="sm">
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
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
          error={errors.unitCost?.message}
        >
          <Input
            id="unitCost"
            type="number"
            step="0.01"
            // valueAsNumber turns an empty field into NaN, not undefined - z.number().optional()
            // rejects NaN, so leaving this genuinely-optional field blank silently failed
            // validation with no visible error. setValueAs maps blank to undefined instead.
            {...register('unitCost', { setValueAs: (v) => (v === '' ? undefined : Number(v)) })}
            error={errors.unitCost?.message}
          />
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
