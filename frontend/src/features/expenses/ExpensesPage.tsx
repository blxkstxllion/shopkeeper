import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Receipt, Plus, ChevronLeft, ChevronRight } from 'lucide-react'
import { getExpenses, getExpenseCategories, deleteExpense } from '@/api/expenses'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { EmptyState } from '@/components/ui/EmptyState'
import { TableSkeleton } from '@/components/ui/Skeleton'
import { formatMoney } from '@/lib/format'
import type { Expense } from '@/types/expense'
import { ExpenseFormModal } from './ExpenseFormModal'

const PAGE_SIZE = 20

export function ExpensesPage() {
  const { branch } = useActiveBranch()
  const queryClient = useQueryClient()
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [page, setPage] = useState(1)
  const [formModal, setFormModal] = useState<{ open: boolean; expense?: Expense | null }>({ open: false })

  useEffect(() => {
    setPage(1)
  }, [from, to, categoryId, branch?.id])

  const { data, isLoading } = useQuery({
    queryKey: ['expenses', { from, to, categoryId, branchId: branch?.id, page }],
    queryFn: () =>
      getExpenses({
        from: from || undefined,
        to: to || undefined,
        categoryId: categoryId || undefined,
        branchId: branch?.id,
        page,
        pageSize: PAGE_SIZE,
      }),
    enabled: Boolean(branch),
  })

  const { data: categories } = useQuery({ queryKey: ['expense-categories'], queryFn: getExpenseCategories })

  const deleteMutation = useMutation({
    mutationFn: deleteExpense,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['expenses'] }),
  })

  const expenses = data?.items ?? []
  const totalPages = data?.totalPages ?? 1
  const totalCount = data?.totalCount ?? 0
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1
  const rangeEnd = Math.min(page * PAGE_SIZE, totalCount)

  return (
    <div className="mx-auto max-w-6xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Expenses</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {branch ? `Expenses at ${branch.name}` : 'Loading branch…'}
          </p>
        </div>
        <Button onClick={() => setFormModal({ open: true, expense: null })} disabled={!branch}>
          <Plus className="h-4 w-4" />
          Add expense
        </Button>
      </div>

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">From</label>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-40" />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">To</label>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-40" />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">Category</label>
          <select
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="h-10 w-48 rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">All categories</option>
            {categories?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={5} rows={6} />
        ) : expenses.length === 0 ? (
          <EmptyState
            icon={Receipt}
            title="No expenses recorded"
            description="Add your first expense to start tracking where your money goes."
            action={
              <Button onClick={() => setFormModal({ open: true, expense: null })} disabled={!branch}>
                <Plus className="h-4 w-4" />
                Add expense
              </Button>
            }
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Date</th>
                  <th className="px-4 py-3 font-medium">Category</th>
                  <th className="px-4 py-3 font-medium">Description</th>
                  <th className="px-4 py-3 font-medium">Branch</th>
                  <th className="px-4 py-3 font-medium text-right">Amount</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {expenses.map((e) => (
                  <tr key={e.id}>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{e.expenseDate}</td>
                    <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{e.categoryName}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{e.description ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{e.branchName ?? 'Business-wide'}</td>
                    <td className="px-4 py-3 text-right text-slate-900 dark:text-slate-100">{formatMoney(e.amount)}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => setFormModal({ open: true, expense: e })}>
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => deleteMutation.mutate(e.id)}
                          isLoading={deleteMutation.isPending && deleteMutation.variables === e.id}
                        >
                          Delete
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {totalCount > 0 && (
        <div className="mt-3 flex items-center justify-between text-sm text-slate-500 dark:text-slate-400">
          <span>
            Showing {rangeStart}–{rangeEnd} of {totalCount}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="secondary" size="sm" onClick={() => setPage((p) => p - 1)} disabled={page <= 1}>
              <ChevronLeft className="h-3.5 w-3.5" />
              Previous
            </Button>
            <span className="px-1">
              Page {page} of {totalPages}
            </span>
            <Button variant="secondary" size="sm" onClick={() => setPage((p) => p + 1)} disabled={page >= totalPages}>
              Next
              <ChevronRight className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      )}

      <ExpenseFormModal
        isOpen={formModal.open}
        onClose={() => setFormModal({ open: false })}
        branchId={branch?.id ?? null}
        expense={formModal.expense}
      />
    </div>
  )
}
