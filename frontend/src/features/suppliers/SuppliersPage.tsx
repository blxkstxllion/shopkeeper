import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Truck, Plus, History, PackagePlus } from 'lucide-react'
import { getSuppliers, deleteSupplier } from '@/api/suppliers'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { EmptyState } from '@/components/ui/EmptyState'
import { TableSkeleton } from '@/components/ui/Skeleton'
import type { Supplier } from '@/types/supplier'
import { SupplierFormModal } from './SupplierFormModal'
import { RestockModal } from './RestockModal'
import { SupplierHistoryModal } from './SupplierHistoryModal'

export function SuppliersPage() {
  const queryClient = useQueryClient()
  const [formModal, setFormModal] = useState<{ open: boolean; supplier?: Supplier | null }>({ open: false })
  const [restockSupplier, setRestockSupplier] = useState<Supplier | null>(null)
  const [historySupplier, setHistorySupplier] = useState<Supplier | null>(null)

  const { data: suppliers, isLoading } = useQuery({ queryKey: ['suppliers'], queryFn: getSuppliers })

  const deleteMutation = useMutation({
    mutationFn: deleteSupplier,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['suppliers'] }),
  })

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Suppliers</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Manage who you buy stock from and record restocks against them.
          </p>
        </div>
        <Button onClick={() => setFormModal({ open: true, supplier: null })}>
          <Plus className="h-4 w-4" />
          Add supplier
        </Button>
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={4} rows={6} />
        ) : !suppliers || suppliers.length === 0 ? (
          <EmptyState
            icon={Truck}
            title="No suppliers yet"
            description="Add a supplier to start tracking where your stock comes from."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Supplier</th>
                  <th className="px-4 py-3 font-medium">Contact</th>
                  <th className="px-4 py-3 font-medium">Phone</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {suppliers.map((s) => (
                  <tr key={s.id}>
                    <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{s.name}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{s.contactName ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{s.phone ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{s.email ?? '—'}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Button variant="ghost" size="sm" onClick={() => setHistorySupplier(s)}>
                          <History className="h-3.5 w-3.5" />
                          History
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setRestockSupplier(s)}>
                          <PackagePlus className="h-3.5 w-3.5" />
                          Restock
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => setFormModal({ open: true, supplier: s })}>
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => deleteMutation.mutate(s.id)}
                          isLoading={deleteMutation.isPending && deleteMutation.variables === s.id}
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

      <SupplierFormModal
        isOpen={formModal.open}
        onClose={() => setFormModal({ open: false })}
        supplier={formModal.supplier}
      />
      <RestockModal
        isOpen={Boolean(restockSupplier)}
        onClose={() => setRestockSupplier(null)}
        supplier={restockSupplier}
      />
      <SupplierHistoryModal
        isOpen={Boolean(historySupplier)}
        onClose={() => setHistorySupplier(null)}
        supplier={historySupplier}
      />
    </div>
  )
}
