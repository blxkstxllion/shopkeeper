import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Lock, Plus, Sparkles, Users } from 'lucide-react'
import { deleteRole, getRoleManagement } from '@/api/roles'
import { getPlanUsage } from '@/api/plans'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { useAuth } from '@/contexts/AuthContext'
import { ApiError } from '@/lib/api-client'
import { RoleFormModal } from './RoleFormModal'
import type { RoleManagement } from '@/types/role'

export function RolesSection() {
  const { activeBusiness } = useAuth()
  const isOwner = activeBusiness?.isOwner ?? false
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingRole, setEditingRole] = useState<RoleManagement | null>(null)

  const { data: roles, isLoading } = useQuery({ queryKey: ['role-management'], queryFn: getRoleManagement })
  const { data: usage } = useQuery({ queryKey: ['plan-usage'], queryFn: getPlanUsage })

  const isEnterpriseTier = usage?.currentTier === 'Enterprise' || usage?.currentTier === 'EnterpriseAi'
  const canManageRoles = isOwner && isEnterpriseTier

  const deleteMutation = useMutation({
    mutationFn: deleteRole,
    onSuccess: () => {
      setServerError(null)
      queryClient.invalidateQueries({ queryKey: ['role-management'] })
    },
    onError: (err) =>
      setServerError(err instanceof ApiError ? err.message : 'Unable to delete this role. Please try again.'),
  })

  function openCreate() {
    setEditingRole(null)
    setFormOpen(true)
  }

  function openEdit(role: RoleManagement) {
    setEditingRole(role)
    setFormOpen(true)
  }

  if (isLoading || !roles) {
    return (
      <Card className="p-4">
        <div className="h-32 animate-pulse" />
      </Card>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <Card className="p-4">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 className="mb-1 text-sm font-semibold text-slate-900 dark:text-slate-100">Roles &amp; permissions</h2>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              The 7 default roles are the same for every plan and can&apos;t be changed. Custom roles let you define
              exactly what a job title can access.
            </p>
          </div>
          {canManageRoles && (
            <Button size="sm" onClick={openCreate}>
              <Plus className="h-3.5 w-3.5" />
              New role
            </Button>
          )}
        </div>

        {!isEnterpriseTier && (
          <div className="mb-4 flex items-center gap-3 rounded-xl border border-primary-200 bg-primary-50/50 p-3 dark:border-primary-800 dark:bg-primary-900/10">
            <Sparkles className="h-4 w-4 shrink-0 text-primary-600" />
            <p className="text-sm text-slate-700 dark:text-slate-300">
              Custom roles are an Enterprise feature.{' '}
              <a
                href="/app/settings?section=subscription"
                className="font-medium text-primary-700 hover:underline dark:text-primary-400"
              >
                Upgrade to unlock
              </a>
              .
            </p>
          </div>
        )}
        {isEnterpriseTier && !isOwner && (
          <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">Only the business owner can manage roles.</p>
        )}

        {serverError && <Alert tone="error">{serverError}</Alert>}

        <div className="flex flex-col divide-y divide-slate-100 dark:divide-slate-800">
          {roles.map((role) => (
            <div key={role.id} className="flex items-center justify-between gap-4 py-3">
              <div>
                <div className="flex items-center gap-1.5">
                  <p className="font-medium text-slate-900 dark:text-slate-100">{role.name}</p>
                  {role.isSystemRole && (
                    <span className="flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                      <Lock className="h-3 w-3" />
                      Default
                    </span>
                  )}
                </div>
                {role.description && <p className="text-sm text-slate-500 dark:text-slate-400">{role.description}</p>}
                <p className="mt-0.5 flex items-center gap-1 text-xs text-slate-400">
                  <Users className="h-3 w-3" />
                  {role.employeeCount} {role.employeeCount === 1 ? 'employee' : 'employees'} ·{' '}
                  {role.permissionKeys.length} permission{role.permissionKeys.length === 1 ? '' : 's'}
                </p>
              </div>
              {!role.isSystemRole && isOwner && (
                <div className="flex shrink-0 gap-2">
                  <Button variant="ghost" size="sm" onClick={() => openEdit(role)}>
                    Edit
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => deleteMutation.mutate(role.id)}
                    isLoading={deleteMutation.isPending && deleteMutation.variables === role.id}
                  >
                    Delete
                  </Button>
                </div>
              )}
            </div>
          ))}
        </div>
      </Card>

      <RoleFormModal isOpen={formOpen} onClose={() => setFormOpen(false)} role={editingRole} />
    </div>
  )
}
