import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Calendar, Trash2, Loader2, Plus } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Alert } from '@/components/ui/Alert'
import { getScheduledReports, createScheduledReport, deleteScheduledReport } from '@/api/reports'
import { useBranchContext } from '@/contexts/BranchContext'
import { ApiError } from '@/lib/api-client'
import type { ScheduledReportFrequency } from '@/types/reports'
import type { ReportExportFormat } from '@/api/reports'

const FREQUENCIES: { value: ScheduledReportFrequency; label: string }[] = [
  { value: 'Daily', label: 'Daily' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
]

/** Manage recurring "email me the business report" subscriptions - separate from
 * GenerateReportButton's one-off download, this is the scheduled/exportable-reports feature:
 * ScheduledReportRunner (a backend background job) emails the same document out automatically. */
export function ScheduledReportsButton() {
  const [isOpen, setIsOpen] = useState(false)

  return (
    <>
      <Button type="button" variant="secondary" size="sm" onClick={() => setIsOpen(true)}>
        <Calendar className="h-3.5 w-3.5" />
        Scheduled reports
      </Button>
      <Modal isOpen={isOpen} onClose={() => setIsOpen(false)} title="Scheduled reports" size="md">
        <ScheduledReportsPanel />
      </Modal>
    </>
  )
}

function ScheduledReportsPanel() {
  const { branches, canSwitchBranches } = useBranchContext()
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [frequency, setFrequency] = useState<ScheduledReportFrequency>('Weekly')
  const [format, setFormat] = useState<ReportExportFormat>('Pdf')
  const [branchId, setBranchId] = useState('')
  const [emails, setEmails] = useState('')

  const { data: reports, isLoading } = useQuery({
    queryKey: ['scheduled-reports'],
    queryFn: getScheduledReports,
  })

  const createMutation = useMutation({
    mutationFn: createScheduledReport,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['scheduled-reports'] })
      setShowForm(false)
      setEmails('')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: deleteScheduledReport,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['scheduled-reports'] }),
  })

  function handleCreate() {
    const recipientEmails = emails
      .split(',')
      .map((e) => e.trim())
      .filter(Boolean)
    createMutation.mutate({ branchId: branchId || undefined, frequency, format, recipientEmails })
  }

  return (
    <div className="flex flex-col gap-4">
      {isLoading ? (
        <Loader2 className="mx-auto h-5 w-5 animate-spin text-slate-400" />
      ) : reports && reports.length > 0 ? (
        <ul className="divide-y divide-slate-100 dark:divide-slate-800">
          {reports.map((r) => (
            <li key={r.id} className="flex items-center justify-between py-2.5 text-sm">
              <div>
                <p className="font-medium text-slate-900 dark:text-slate-100">
                  {r.frequency} · {r.format} · {r.branchName ?? 'All branches'}
                </p>
                <p className="text-xs text-slate-400">
                  {r.recipientEmails.join(', ')} · next {new Date(r.nextRunAt).toLocaleDateString()}
                </p>
              </div>
              <button
                type="button"
                onClick={() => deleteMutation.mutate(r.id)}
                disabled={deleteMutation.isPending}
                aria-label="Delete schedule"
                className="shrink-0 rounded-lg p-1.5 text-slate-400 hover:bg-danger/10 hover:text-danger disabled:opacity-50 dark:hover:text-danger-dark"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-slate-400">No scheduled reports yet.</p>
      )}

      {showForm ? (
        <div className="flex flex-col gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-800">
          <div className="flex gap-2">
            <select
              value={frequency}
              onChange={(e) => setFrequency(e.target.value as ScheduledReportFrequency)}
              className="h-9 flex-1 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
            >
              {FREQUENCIES.map((f) => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </select>
            <select
              value={format}
              onChange={(e) => setFormat(e.target.value as ReportExportFormat)}
              className="h-9 flex-1 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
            >
              <option value="Pdf">PDF</option>
              <option value="Word">Word</option>
            </select>
          </div>
          {canSwitchBranches && (
            <select
              value={branchId}
              onChange={(e) => setBranchId(e.target.value)}
              className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
            >
              <option value="">All branches</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          )}
          <input
            type="text"
            value={emails}
            onChange={(e) => setEmails(e.target.value)}
            placeholder="you@example.com, teammate@example.com"
            className="h-9 rounded-lg border border-slate-300 bg-white px-2 text-sm dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          />
          {createMutation.isError && (
            <Alert tone="error">
              {createMutation.error instanceof ApiError
                ? createMutation.error.message
                : 'Could not create the schedule.'}
            </Alert>
          )}
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={() => setShowForm(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={handleCreate}
              disabled={createMutation.isPending || !emails.trim()}
            >
              {createMutation.isPending ? 'Creating…' : 'Create'}
            </Button>
          </div>
        </div>
      ) : (
        <Button type="button" variant="secondary" size="sm" onClick={() => setShowForm(true)}>
          <Plus className="h-3.5 w-3.5" />
          New schedule
        </Button>
      )}
    </div>
  )
}
