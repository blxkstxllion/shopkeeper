import { useState } from 'react'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { formatDateTime } from '@/lib/format'
import { useOfflineMutation } from '@/offline/useOfflineMutation'
import type { JoinRequestItem } from '@/types/employee'
import { ApproveJoinRequestModal } from './ApproveJoinRequestModal'

export function JoinRequestsSection({ requests }: { requests: JoinRequestItem[] }) {
  const [approving, setApproving] = useState<JoinRequestItem | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const rejectMutation = useOfflineMutation<{ id: string }>('joinRequestReject', () => 'Reject join request')

  if (requests.length === 0) return null

  return (
    <>
      <h2 className="mb-3 mt-6 text-sm font-medium text-slate-700 dark:text-slate-300">Join requests</h2>
      {actionError && (
        <div className="mb-3">
          <Alert tone="error">{actionError}</Alert>
        </div>
      )}
      <Card className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                <th className="px-4 py-3 font-medium">Name</th>
                <th className="px-4 py-3 font-medium">Email</th>
                <th className="px-4 py-3 font-medium">Phone</th>
                <th className="px-4 py-3 font-medium">Requested</th>
                <th className="px-4 py-3 font-medium"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {requests.map((r) => (
                <tr key={r.id}>
                  <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">
                    {r.firstName} {r.lastName}
                  </td>
                  <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{r.email}</td>
                  <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{r.phone ?? '—'}</td>
                  <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{formatDateTime(r.requestedAt)}</td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex justify-end gap-2">
                      <Button variant="ghost" size="sm" onClick={() => setApproving(r)}>
                        Approve
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          rejectMutation.mutate(
                            { payload: { id: r.id } },
                            {
                              onSuccess: () => setActionError(null),
                              onError: (err) =>
                                setActionError(
                                  err instanceof ApiError
                                    ? err.message
                                    : 'Unable to reject this request. Please try again.',
                                ),
                            },
                          )
                        }
                        isLoading={rejectMutation.isPending && rejectMutation.variables?.payload.id === r.id}
                      >
                        Reject
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <ApproveJoinRequestModal isOpen={Boolean(approving)} onClose={() => setApproving(null)} request={approving} />
    </>
  )
}
