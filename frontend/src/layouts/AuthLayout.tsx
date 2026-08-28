import { Outlet } from 'react-router-dom'
import { Store } from 'lucide-react'

export function AuthLayout() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-12 dark:bg-slate-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-600 text-white">
            <Store className="h-6 w-6" />
          </div>
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</p>
          <p className="text-sm text-slate-500 dark:text-slate-400">Know Your Business. Grow Your Profit.</p>
        </div>
        <Outlet />
      </div>
    </div>
  )
}
