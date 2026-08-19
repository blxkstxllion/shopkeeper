import { useQuery } from '@tanstack/react-query'
import { PackageSearch } from 'lucide-react'
import { Modal } from '@/components/ui/Modal'
import { EmptyState } from '@/components/ui/EmptyState'
import { getSupplierRestockHistory } from '@/api/suppliers'
import { formatDateTime } from '@/lib/format'
import type { Supplier } from '@/types/supplier'

export function SupplierHistoryModal({
  isOpen,
  onClose,
  supplier,
}: {
  isOpen: boolean
  onClose: () => void
  supplier: Supplier | null
}) {
  const { data: history, isLoading } = useQuery({
    queryKey: ['supplier-restocks', supplier?.id],
    queryFn: () => getSupplierRestockHistory(supplier!.id),
    enabled: isOpen && Boolean(supplier),
  })

  if (!supplier) return null

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={`Restock history — ${supplier.name}`} size="lg">
      {isLoading ? (
        <div className="p-2 text-sm text-slate-400">Loading…</div>
      ) : !history || history.length === 0 ? (
        <EmptyState
          icon={PackageSearch}
          title="No restocks yet"
          description="Restocks from this supplier will show up here."
        />
      ) : (
        <div className="max-h-96 overflow-y-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                <th className="px-3 py-2 font-medium">Product</th>
                <th className="px-3 py-2 font-medium">Branch</th>
                <th className="px-3 py-2 font-medium">Quantity</th>
                <th className="px-3 py-2 font-medium">By</th>
                <th className="px-3 py-2 font-medium">Date</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {history.map((h) => (
                <tr key={h.id}>
                  <td className="px-3 py-2 font-medium text-slate-900 dark:text-slate-100">{h.productName}</td>
                  <td className="px-3 py-2 text-slate-500 dark:text-slate-400">{h.branchName}</td>
                  <td className="px-3 py-2 text-good dark:text-good-dark">+{h.quantity}</td>
                  <td className="px-3 py-2 text-slate-500 dark:text-slate-400">{h.createdByName}</td>
                  <td className="px-3 py-2 text-slate-500 dark:text-slate-400">{formatDateTime(h.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  )
}
