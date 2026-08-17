import { Link } from 'react-router-dom'
import { Sparkles } from 'lucide-react'

export function UpgradePrompt({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-primary-300 bg-primary-50/50 px-6 py-16 text-center dark:border-primary-800 dark:bg-primary-900/10">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary-100 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400">
        <Sparkles className="h-6 w-6" />
      </div>
      <h3 className="text-base font-semibold text-slate-900 dark:text-slate-100">{title}</h3>
      <p className="max-w-sm text-sm text-slate-500 dark:text-slate-400">{description}</p>
      <Link
        to="/app/settings?section=subscription"
        className="mt-1 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700"
      >
        Upgrade to unlock
      </Link>
    </div>
  )
}
