import { AlertTriangle } from 'lucide-react'
import { Modal } from './Modal'
import { Button } from './Button'

/** A "Are you sure?" step for destructive actions (delete role, delete expense, delete
 * scheduled report, ...) - without this, those actions fired immediately on click with only
 * a loading spinner as feedback, a real risk of accidental data loss for non-soft-deletes. */
export function ConfirmDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = 'Delete',
  isConfirming = false,
}: {
  isOpen: boolean
  onClose: () => void
  onConfirm: () => void
  title: string
  description: string
  confirmLabel?: string
  isConfirming?: boolean
}) {
  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} size="sm">
      <div className="flex gap-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-danger dark:text-danger-dark" />
        <p className="text-sm text-slate-600 dark:text-slate-400">{description}</p>
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onClose} disabled={isConfirming}>
          Cancel
        </Button>
        <Button type="button" variant="danger" size="sm" onClick={onConfirm} isLoading={isConfirming}>
          {confirmLabel}
        </Button>
      </div>
    </Modal>
  )
}
