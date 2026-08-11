import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Building2, Plus, BarChart3 } from 'lucide-react'
import { getBranches, deleteBranch } from '@/api/branches'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { EmptyState } from '@/components/ui/EmptyState'
import type { Branch } from '@/types/business'
import { BranchFormModal } from './BranchFormModal'

export function BranchesPage() {
  const queryClient = useQueryClient()
  const [formModal, setFormModal] = useState<{ open: boolean; branch?: Branch | null }>({ open: false })

  const { data: branches, isLoading } = useQuery({ queryKey: ['branches'], queryFn: getBranches })

  const deleteMutation = useMutation({
    mutationFn: deleteBranch,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branches'] }),
  })

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Branches</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">Manage your business's locations.</p>
        </div>
        <Button onClick={() => setFormModal({ open: true, branch: null })}>
          <Plus className="h-4 w-4" />
          Add branch
        </Button>
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <div className="p-6 text-sm text-slate-400">Loading…</div>
        ) : !branches || branches.length === 0 ? (
          <EmptyState
            icon={Building2}
            title="No branches yet"
            description="Add a branch to start tracking sales and inventory there."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Branch</th>
                  <th className="px-4 py-3 font-medium">Code</th>
                  <th className="px-4 py-3 font-medium">Location</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {branches.map((b) => (
                  <tr key={b.id}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-slate-900 dark:text-slate-100">{b.name}</span>
                        {b.isMainBranch && (
                          <span className="rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
                            Main
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{b.code}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{b.city ?? '—'}</td>
                    <td className="px-4 py-3">
                      <span className={b.isActive ? 'text-[#006300] dark:text-[#0ca30c]' : 'text-slate-400'}>
                        {b.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Link
                          to={`/app/reports?branchId=${b.id}`}
                          className="inline-flex h-8 items-center gap-1.5 rounded-lg px-3 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
                        >
                          <BarChart3 className="h-3.5 w-3.5" />
                          Reports
                        </Link>
                        <Button variant="ghost" size="sm" onClick={() => setFormModal({ open: true, branch: b })}>
                          Edit
                        </Button>
                        {b.isActive && !b.isMainBranch && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => deleteMutation.mutate(b.id)}
                            isLoading={deleteMutation.isPending && deleteMutation.variables === b.id}
                          >
                            Deactivate
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <BranchFormModal
        isOpen={formModal.open}
        onClose={() => setFormModal({ open: false })}
        branch={formModal.branch}
      />
    </div>
  )
}
