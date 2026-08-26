import type { ReactNode } from 'react'
import { AlertTriangle, CheckCircle2, Info } from 'lucide-react'
import { clsx } from 'clsx'

type Tone = 'error' | 'success' | 'info'

const toneStyles: Record<Tone, { wrap: string; icon: typeof Info }> = {
  error: {
    wrap: 'bg-red-50 text-red-800 border-red-200 dark:bg-red-950/40 dark:text-red-300 dark:border-red-900',
    icon: AlertTriangle,
  },
  success: {
    wrap: 'bg-primary-50 text-primary-800 border-primary-200 dark:bg-primary-950/40 dark:text-primary-300 dark:border-primary-900',
    icon: CheckCircle2,
  },
  info: {
    wrap: 'bg-blue-50 text-blue-800 border-blue-200 dark:bg-blue-950/40 dark:text-blue-300 dark:border-blue-900',
    icon: Info,
  },
}

export function Alert({ tone = 'info', children }: { tone?: Tone; children: ReactNode }) {
  const { wrap, icon: Icon } = toneStyles[tone]
  return (
    <div className={clsx('flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm', wrap)}>
      <Icon className="mt-0.5 h-4 w-4 shrink-0" />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  )
}
