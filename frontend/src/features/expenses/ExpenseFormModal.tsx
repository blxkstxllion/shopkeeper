import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, FormField } from '@/components/ui/Input'
import { Alert } from '@/components/ui/Alert'
import { getExpenseCategories } from '@/api/expenses'
import { ApiError } from '@/lib/api-client'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import { useOfflineListQuery } from '@/offline/useOfflineQuery'
import type { Expense, ExpenseCategory } from '@/types/expense'

const schema = z.object({
  expenseCategoryId: z.string().min(1, 'Choose a category'),
  amount: z.number().positive('Amount must be greater than zero'),
  expenseDate: z.string().min(1, 'Date is required'),
  description: z.string().max(500).optional().or(z.literal('')),
  businessWide: z.boolean(),
})

type FormValues = z.infer<typeof schema>

const defaults: FormValues = {
  expenseCategoryId: '',
  amount: 0,
  expenseDate: new Date().toISOString().slice(0, 10),
  description: '',
  businessWide: false,
}

export function ExpenseFormModal({
  isOpen,
  onClose,
  branchId,
  expense,
}: {
  isOpen: boolean
  onClose: () => void
  branchId: string | null
  expense?: Expense | null
}) {
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [isAddingCategory, setIsAddingCategory] = useState(false)
  const [newCategoryName, setNewCategoryName] = useState('')
  const isEditing = Boolean(expense)

  const { data: categories } = useOfflineListQuery<ExpenseCategory>(
    ['expense-categories'],
    'expenseCategories',
    getExpenseCategories,
  )

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    reset,
    setValue,
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: expense
      ? {
          expenseCategoryId: expense.expenseCategoryId,
          amount: expense.amount,
          expenseDate: expense.expenseDate,
          description: expense.description ?? '',
          businessWide: expense.branchId === null,
        }
      : defaults,
  })

  useEffect(() => {
    if (isOpen) {
      setServerError(null)
      setIsAddingCategory(false)
      setNewCategoryName('')
    }
  }, [isOpen])

  const categoryMutation = useOfflineMutation<{ name: string }>(
    'expenseCategory',
    (payload) => `New category: ${payload.name}`,
  )
  async function handleAddCategory() {
    setServerError(null)
    try {
      const result = await categoryMutation.mutateAsync({ payload: { name: newCategoryName } })
      if (!result.queued) {
        await queryClient.invalidateQueries({ queryKey: ['expense-categories'] })
        setValue('expenseCategoryId', (result.data as { id: string }).id, { shouldValidate: true })
      }
      setIsAddingCategory(false)
      setNewCategoryName('')
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to create that category. Please try again.')
    }
  }

  const createMutation = useOfflineMutation<{
    branchId: string | null
    expenseCategoryId: string
    amount: number
    expenseDate: string
    description: string | null
  }>('expense', (payload) => `New expense: ${payload.amount.toFixed(2)}`)
  const updateMutation = useOfflineMutation<{
    branchId: string | null
    expenseCategoryId: string
    amount: number
    expenseDate: string
    description: string | null
    id: string
  }>('expenseUpdate', (payload) => `Update expense: ${payload.amount.toFixed(2)}`)
  const isSaving = createMutation.isPending || updateMutation.isPending

  async function onSubmit(values: FormValues) {
    setServerError(null)
    const payload = {
      branchId: values.businessWide ? null : branchId,
      expenseCategoryId: values.expenseCategoryId,
      amount: values.amount,
      expenseDate: values.expenseDate,
      description: values.description || null,
    }
    try {
      if (isEditing && expense) {
        await updateMutation.mutateAsync({ payload: { ...payload, id: expense.id } })
      } else {
        await createMutation.mutateAsync({ payload })
      }
      reset(defaults)
      onClose()
    } catch (err) {
      setServerError(err instanceof ApiError ? err.message : 'Unable to save this expense. Please try again.')
    }
  }

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isEditing ? 'Edit expense' : 'Add expense'}>
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        {serverError && <Alert tone="error">{serverError}</Alert>}

        <FormField label="Category" htmlFor="expenseCategoryId" error={errors.expenseCategoryId?.message}>
          {isAddingCategory ? (
            <div className="flex gap-2">
              <Input
                autoFocus
                placeholder="New category name"
                value={newCategoryName}
                onChange={(e) => setNewCategoryName(e.target.value)}
              />
              <Button
                type="button"
                size="sm"
                isLoading={categoryMutation.isPending}
                disabled={!newCategoryName.trim()}
                onClick={() => void handleAddCategory()}
              >
                Add
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => setIsAddingCategory(false)}>
                Cancel
              </Button>
            </div>
          ) : (
            <div className="flex gap-2">
              <select
                id="expenseCategoryId"
                {...register('expenseCategoryId')}
                className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
              >
                <option value="">Select a category</option>
                {categories?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              <Button type="button" variant="secondary" size="sm" onClick={() => setIsAddingCategory(true)}>
                <Plus className="h-3.5 w-3.5" />
                New
              </Button>
            </div>
          )}
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Amount" htmlFor="amount" error={errors.amount?.message}>
            <Input
              id="amount"
              type="number"
              step="0.01"
              {...register('amount', { valueAsNumber: true })}
              error={errors.amount?.message}
            />
          </FormField>
          <FormField label="Date" htmlFor="expenseDate" error={errors.expenseDate?.message}>
            <Input id="expenseDate" type="date" {...register('expenseDate')} error={errors.expenseDate?.message} />
          </FormField>
        </div>

        <FormField label="Description (optional)" htmlFor="description" error={errors.description?.message}>
          <Input id="description" {...register('description')} error={errors.description?.message} />
        </FormField>

        <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
          <input
            type="checkbox"
            {...register('businessWide')}
            className="h-4 w-4 rounded border-slate-300 text-primary-600"
          />
          Business-wide expense (not tied to this branch)
        </label>

        <div className="mt-2 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting || isSaving}>
            {isEditing ? 'Save changes' : 'Add expense'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
