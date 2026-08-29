import { Outlet } from 'react-router-dom'
import { Logo } from '@/components/ui/Logo'

export function AuthLayout() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <Logo className="h-11 w-11" />
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</p>
          <p className="text-sm text-slate-500 dark:text-slate-400">Know Your Business. Grow Your Profit.</p>
        </div>
        <Outlet />
      </div>
    </div>
  )
}
