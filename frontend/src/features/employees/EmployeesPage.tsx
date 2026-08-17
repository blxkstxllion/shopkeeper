import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Users, Plus, Clock } from 'lucide-react'
import { getBusinessUsers, removeEmployee } from '@/api/employees'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { EmptyState } from '@/components/ui/EmptyState'
import { ApiError } from '@/lib/api-client'
import { formatDateTime } from '@/lib/format'
import { InviteEmployeeModal } from './InviteEmployeeModal'
import { JoinCodeCard } from './JoinCodeCard'
import { JoinRequestsSection } from './JoinRequestsSection'

export function EmployeesPage() {
  const queryClient = useQueryClient()
  const [inviteOpen, setInviteOpen] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({ queryKey: ['business-users'], queryFn: getBusinessUsers })

  const removeMutation = useMutation({
    mutationFn: removeEmployee,
    onSuccess: () => {
      setActionError(null)
      queryClient.invalidateQueries({ queryKey: ['business-users'] })
    },
    onError: (err) => {
      setActionError(err instanceof ApiError ? err.message : 'Unable to remove this team member. Please try again.')
    },
  })

  const members = data?.members ?? []
  const pending = data?.pendingInvitations ?? []

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Employees</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">Manage who has access to your business.</p>
        </div>
        <Button onClick={() => setInviteOpen(true)}>
          <Plus className="h-4 w-4" />
          Invite
        </Button>
      </div>

      {actionError && (
        <div className="mb-4">
          <Alert tone="error">{actionError}</Alert>
        </div>
      )}

      <JoinCodeCard />

      <Card className="mb-6 overflow-hidden">
        {isLoading ? (
          <div className="p-6 text-sm text-slate-400">Loading…</div>
        ) : members.length === 0 ? (
          <EmptyState icon={Users} title="No team members yet" description="Invite someone to get started." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">Name</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium">Role</th>
                  <th className="px-4 py-3 font-medium">Branch</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {members.map((m) => (
                  <tr key={m.businessUserId}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-slate-900 dark:text-slate-100">
                          {m.firstName} {m.lastName}
                        </span>
                        {m.isOwner && (
                          <span className="rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
                            Owner
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{m.email}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{m.roleName}</td>
                    <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{m.branchName ?? 'All branches'}</td>
                    <td className="px-4 py-3">
                      <span
                        className={
                          m.status === 'Active'
                            ? 'text-[#006300] dark:text-[#0ca30c]'
                            : 'text-amber-600 dark:text-amber-400'
                        }
                      >
                        {m.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {!m.isOwner && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => removeMutation.mutate(m.businessUserId)}
                          isLoading={removeMutation.isPending && removeMutation.variables === m.businessUserId}
                        >
                          Remove
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {pending.length > 0 && (
        <>
          <h2 className="mb-3 text-sm font-medium text-slate-700 dark:text-slate-300">Pending invitations</h2>
          <Card className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                    <th className="px-4 py-3 font-medium">Email</th>
                    <th className="px-4 py-3 font-medium">Role</th>
                    <th className="px-4 py-3 font-medium">Branch</th>
                    <th className="px-4 py-3 font-medium">Invited</th>
                    <th className="px-4 py-3 font-medium">Expires</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {pending.map((p) => (
                    <tr key={p.id}>
                      <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{p.email}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{p.roleName}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{p.branchName ?? 'All branches'}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{formatDateTime(p.invitedAt)}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">
                        <span className="inline-flex items-center gap-1">
                          <Clock className="h-3.5 w-3.5" />
                          {formatDateTime(p.expiresAt)}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      )}

      <JoinRequestsSection requests={data?.joinRequests ?? []} />

      <InviteEmployeeModal isOpen={inviteOpen} onClose={() => setInviteOpen(false)} />
    </div>
  )
}
