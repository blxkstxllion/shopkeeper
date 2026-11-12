import { AlertCircle, RefreshCw } from 'lucide-react'

/** Shown instead of (or above) a list/page's empty state when the query actually failed
 * (isError) rather than genuinely returning nothing - without this, pages that only check
 * isLoading/data render "No X yet" on a real server error, indistinguishable from an empty
 * business. Extracted from DashboardPage, the one screen that already did this correctly. */
export function QueryErrorBanner({ onRetry, isRetrying = false }: { onRetry: () => void; isRetrying?: boolean }) {
  return (
    <div className="mb-4 flex animate-[fade-up_0.4s_ease-out_both] flex-wrap items-center gap-3 rounded-xl border border-danger/20 bg-danger/5 px-4 py-3 text-sm text-danger dark:border-danger-dark/30 dark:bg-danger-dark/10 dark:text-danger-dark">
      <AlertCircle className="h-4 w-4 shrink-0" />
      <span className="flex-1">Unable to reach the server. Please check your connection and try again.</span>
      <button
        type="button"
        onClick={onRetry}
        disabled={isRetrying}
        className="inline-flex items-center gap-1.5 rounded-lg bg-danger px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-danger/90 disabled:opacity-60 dark:bg-danger-dark dark:text-slate-900"
      >
        <RefreshCw className={`h-3.5 w-3.5 ${isRetrying ? 'animate-spin' : ''}`} />
        Retry
      </button>
    </div>
  )
}
