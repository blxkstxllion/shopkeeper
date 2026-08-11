import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { z } from 'zod'
import { AxiosError } from 'axios'
import { useState } from 'react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { adjustStock } from '@/api/inventory'
import type { ApiErrorPayload } from '@/types/auth'
import type { Product } from '@/types/product'

const schema = z.object({
  quantityChange: z.number().int().refine((v) => v !== 0, 'Enter a non-zero amount'),
  reason: z.string().min(1, 'A reason is required'),
})

type FormValues = z.infer<typeof schema>

export function StockAdjustModal({
  isOpen,
  onClose,
  product,
  branchId,
}: {
  isOpen: boolean
  onClose: () => void
  product: Product | null
  branchId: string | null
}) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { quantityChange: 0, reason: '' } })

  const mutation = useMutation({
    mutationFn: (values: FormValues) => {
      if (!product || !branchId) throw new Error('Missing product or branch')
      return adjustStock({ productId: product.id, branchId, quantityChange: values.quantityChange, reason: values.reason })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      reset()
      onClose()
    },
    onError: (err) => {
      const apiErr = (err as AxiosError<ApiErrorPayload>).response?.data
      setServerError(apiErr?.title ?? 'Unable to adjust stock. Please try again.')
    },
  })

  if (!product) return null

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={`Adjust stock — ${product.name}`} size="sm">
      <form onSubmit={handleSubmit((values) => mutation.mutate(values))} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <p className="text-sm text-slate-500 dark:text-slate-400">
          Currently <span className="font-medium text-slate-900 dark:text-slate-100">{product.quantityOnHand ?? 0}</span> units on hand.
        </p>

        <FormField
          label="Quantity change"
          htmlFor="quantityChange"
          error={errors.quantityChange?.message}
          hint="Positive to add stock, negative to remove (e.g. -5 for damaged goods)."
        >
          <Input id="quantityChange" type="number" {...register('quantityChange', { valueAsNumber: true })} error={errors.quantityChange?.message} />
        </FormField>

        <FormField label="Reason" htmlFor="reason" error={errors.reason?.message}>
          <Input id="reason" placeholder="e.g. Stock count correction, damaged goods" {...register('reason')} error={errors.reason?.message} />
        </FormField>

        <div className="mt-1 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || mutation.isPending}>
            Save adjustment
          </Button>
        </div>
      </form>
    </Modal>
  )
}
