import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { UserCircle, Plus, Search, ChevronLeft, ChevronRight } from 'lucide-react'
import { getCustomers, deleteCustomer } from '@/api/customers'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { EmptyState } from '@/components/ui/EmptyState'
import { TableSkeleton } from '@/components/ui/Skeleton'
import type { Customer } from '@/types/customer'
import { CustomerFormModal } from './CustomerFormModal'
import { CustomerDetailModal } from './CustomerDetailModal'

const PAGE_SIZE = 20

export function CustomersPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [formModal, setFormModal] = useState<{ open: boolean; customer?: Customer | null }>({ open: false })
  const [detailCustomerId, setDetailCustomerId] = useState<string | null>(null)

  useEffect(() => {
    setPage(1)
  }, [search])

  const { data, isLoading } = useQuery({
    queryKey: ['customers', { search, page }],
    queryFn: () => getCustomers({ search: search || undefined, activeOnly: true, page, pageSize: PAGE_SIZE }),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteCustomer,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customers'] }),
  })

  const customers = data?.items ?? []
  const totalPages = data?.totalPages ?? 1
  const totalCount = data?.totalCount ?? 0
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1
  const rangeEnd = Math.min(page * PAGE_SIZE, totalCount)

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Customers</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Keep track of who's buying and how much they've spent.
          </p>
        </div>
        <Button onClick={() => setFormModal({ open: true, customer: null })}>
          <Plus className="h-4 w-4" />
          Add customer
        </Button>
      </div>

      <div className="mb-4 relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <Input
          placeholder="Search by name, phone, or email…"
          aria-label="Search by name, phone, or email"
          className="pl-9"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={3} rows={6} />
        ) : customers.length === 0 ? (
          <EmptyState
            icon={UserCircle}
            title="No customers yet"
            description="Add a customer to start tracking who's buying from you."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Customer</th>
                  <th className="px-4 py-3 font-medium">Phone</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {customers.map((c) => (
                  <tr key={c.id}>
                    <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{c.name}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{c.phone ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{c.email ?? '—'}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => setDetailCustomerId(c.id)}>
                          View
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setFormModal({ open: true, customer: c })}>
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => deleteMutation.mutate(c.id)}
                          isLoading={deleteMutation.isPending && deleteMutation.variables === c.id}
                        >
                          Deactivate
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

      <CustomerFormModal
        isOpen={formModal.open}
        onClose={() => setFormModal({ open: false })}
        customer={formModal.customer}
      />
      <CustomerDetailModal
        isOpen={Boolean(detailCustomerId)}
        onClose={() => setDetailCustomerId(null)}
        customerId={detailCustomerId}
      />
    </div>
  )
}
