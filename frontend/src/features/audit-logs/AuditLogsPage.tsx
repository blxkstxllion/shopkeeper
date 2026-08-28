import { Fragment, useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { History, ChevronLeft, ChevronRight } from 'lucide-react'
import { getAuditLogs } from '@/api/auditLogs'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { EmptyState } from '@/components/ui/EmptyState'
import { TableSkeleton } from '@/components/ui/Skeleton'
import { formatDateTime } from '@/lib/format'

const PAGE_SIZE = 30

export function AuditLogsPage() {
  const [entityType, setEntityType] = useState('')
  const [action, setAction] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [expandedId, setExpandedId] = useState<string | null>(null)

  useEffect(() => {
    setPage(1)
  }, [entityType, action, from, to])

  const { data, isLoading } = useQuery({
    queryKey: ['audit-logs', { entityType, action, from, to, page }],
    queryFn: () =>
      getAuditLogs({
        entityType: entityType || undefined,
        action: action || undefined,
        from: from || undefined,
        to: to || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
  })

  const logs = data?.items ?? []
  const totalPages = data?.totalPages ?? 1
  const totalCount = data?.totalCount ?? 0
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * PAGE_SIZE + 1
  const rangeEnd = Math.min(page * PAGE_SIZE, totalCount)

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Audit Logs</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Every change made to your business, who made it, and when.
        </p>
      </div>

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">Entity type</label>
          <Input
            placeholder="e.g. Product, Employee"
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
            className="w-44"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">Action</label>
          <Input
            placeholder="e.g. CreateProduct"
            value={action}
            onChange={(e) => setAction(e.target.value)}
            className="w-44"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">From</label>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-40" />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-500 dark:text-slate-400">To</label>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-40" />
        </div>
      </div>

      <Card className="overflow-hidden">
        {isLoading ? (
          <TableSkeleton columns={5} rows={8} />
        ) : logs.length === 0 ? (
          <EmptyState
            icon={History}
            title="No audit log entries"
            description="Changes to your business will show up here as they happen."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                  <th className="px-4 py-3 font-medium">When</th>
                  <th className="px-4 py-3 font-medium">Action</th>
                  <th className="px-4 py-3 font-medium">Entity</th>
                  <th className="px-4 py-3 font-medium">By</th>
                  <th className="px-4 py-3 font-medium">IP</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {logs.map((log) => (
                  <Fragment key={log.id}>
                    <tr>
                      <td className="whitespace-nowrap px-4 py-3 text-slate-500 dark:text-slate-400">
                        {formatDateTime(log.createdAt)}
                      </td>
                      <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{log.action}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{log.entityType ?? '—'}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{log.actorName ?? 'System'}</td>
                      <td className="px-4 py-3 text-slate-500 dark:text-slate-400">{log.ipAddress ?? '—'}</td>
                      <td className="px-4 py-3 text-right">
                        {log.newValue && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setExpandedId(expandedId === log.id ? null : log.id)}
                          >
                            {expandedId === log.id ? 'Hide' : 'Details'}
                          </Button>
                        )}
                      </td>
                    </tr>
                    {expandedId === log.id && log.newValue && (
                      <tr>
                        <td colSpan={6} className="bg-slate-50 px-4 py-3 dark:bg-slate-900">
                          <pre className="overflow-x-auto whitespace-pre-wrap break-all text-xs text-slate-600 dark:text-slate-400">
                            {formatJson(log.newValue)}
                          </pre>
                        </td>
                      </tr>
                    )}
                  </Fragment>
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
    </div>
  )
}

function formatJson(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
