import { TwoFactorSection } from './TwoFactorSection'
import { SessionsSection } from './SessionsSection'

export function SettingsPage() {
  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Settings</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">Manage your account security.</p>
      </div>

      <div className="flex flex-col gap-4">
        <TwoFactorSection />
        <SessionsSection />
      </div>

      <p className="mt-6 text-center text-xs text-slate-400">
        Business, branch, tax, and notification settings arrive in a later phase.
      </p>
    </div>
  )
}
