import { type ReactNode, useEffect, useId, useRef } from 'react'
import { X } from 'lucide-react'
import { clsx } from 'clsx'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

export function Modal({
  isOpen,
  onClose,
  title,
  children,
  size = 'md',
}: {
  isOpen: boolean
  onClose: () => void
  title: string
  children: ReactNode
  size?: 'sm' | 'md' | 'lg'
}) {
  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)
  const previouslyFocused = useRef<HTMLElement | null>(null)
  const wasOpen = useRef(false)

  // Captured during render (not an effect) so it runs before the dialog's own DOM commits -
  // a child with `autoFocus` (e.g. DigitCodeInput) steals document.activeElement during the
  // mutation phase, which happens before any useEffect runs, so an effect-based capture would
  // record the child input instead of whatever element actually triggered the modal.
  if (isOpen && !wasOpen.current) {
    previouslyFocused.current = document.activeElement as HTMLElement | null
  }
  wasOpen.current = isOpen

  useEffect(() => {
    if (!isOpen) return

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose()
        return
      }
      if (e.key !== 'Tab' || !dialogRef.current) return

      // Keeps Tab/Shift+Tab cycling within the dialog instead of escaping into the
      // (visually hidden but still-present) page behind it.
      const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR))
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [isOpen, onClose])

  useEffect(() => {
    if (!isOpen) return

    const firstFocusable = dialogRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)
    // Falls back to the dialog container itself (tabIndex -1, set below) when a modal opens
    // with nothing focusable yet (e.g. a form still loading its fields) - the dialog role
    // still needs to receive focus so a screen reader announces it immediately.
    ;(firstFocusable ?? dialogRef.current)?.focus()

    return () => {
      previouslyFocused.current?.focus()
    }
  }, [isOpen])

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-slate-900/50" onClick={onClose} aria-hidden="true" />
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={clsx(
          'relative max-h-[85vh] w-full overflow-y-auto rounded-2xl bg-white p-5 shadow-xl outline-none dark:bg-slate-900',
          size === 'sm' && 'max-w-sm',
          size === 'md' && 'max-w-lg',
          size === 'lg' && 'max-w-2xl',
        )}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 id={titleId} className="text-base font-semibold text-slate-900 dark:text-slate-100">
            {title}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
