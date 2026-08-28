import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Laptop, LogOut } from 'lucide-react'
import * as authApi from '@/api/auth'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { ListSkeleton } from '@/components/ui/Skeleton'
import { formatDateTime } from '@/lib/format'

function describeDevice(userAgent: string | null): string {
  if (!userAgent) return 'Unknown device'
  if (/edg/i.test(userAgent)) return 'Edge'
  if (/chrome/i.test(userAgent)) return 'Chrome'
  if (/firefox/i.test(userAgent)) return 'Firefox'
  if (/safari/i.test(userAgent)) return 'Safari'
  return userAgent.length > 40 ? `${userAgent.slice(0, 40)}…` : userAgent
}

export function SessionsSection() {
  const queryClient = useQueryClient()
  const { data: sessions, isLoading } = useQuery({ queryKey: ['sessions'], queryFn: authApi.getSessions })

  const revoke = useMutation({
    mutationFn: authApi.revokeSession,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions'] }),
  })

  const revokeOthers = useMutation({
    mutationFn: authApi.revokeOtherSessions,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sessions'] }),
  })

  const hasOtherSessions = (sessions?.length ?? 0) > 1

  return (
    <Card className="p-4">
      <div className="mb-3 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-slate-900 dark:text-slate-100">Active sessions</h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">Devices currently signed in to your account.</p>
        </div>
        {hasOtherSessions && (
          <Button variant="ghost" size="sm" isLoading={revokeOthers.isPending} onClick={() => revokeOthers.mutate()}>
            <LogOut className="h-3.5 w-3.5" />
            Sign out of other sessions
          </Button>
        )}
      </div>

      {isLoading ? (
        <ListSkeleton rows={3} withAvatar />
      ) : (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {sessions?.map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-3 py-3">
              <div className="flex items-center gap-3">
                <div className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-100 text-slate-400 dark:bg-slate-800">
                  <Laptop className="h-4 w-4" />
                </div>
                <div>
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    {describeDevice(s.userAgent)}
                    {s.isCurrent && (
                      <span className="ml-2 rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
                        This device
                      </span>
                    )}
                  </p>
                  <p className="text-xs text-slate-400">
                    {s.createdByIp ?? 'Unknown IP'} · Signed in {formatDateTime(s.createdAt)}
                  </p>
                </div>
              </div>
              {!s.isCurrent && (
                <Button variant="ghost" size="sm" isLoading={revoke.isPending} onClick={() => revoke.mutate(s.id)}>
                  Sign out
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
