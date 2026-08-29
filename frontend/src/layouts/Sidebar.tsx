import { NavLink } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Sparkles } from 'lucide-react'
import { clsx } from 'clsx'
import { navItems } from '@/config/navigation'
import { Logo } from '@/components/ui/Logo'
import { getPlanUsage } from '@/api/plans'

function UpgradeToProCard() {
  const { data } = useQuery({ queryKey: ['plan-usage'], queryFn: getPlanUsage })

  // Already on a paid tier, or billing isn't configured for this deployment - nothing to upsell.
  if (!data || data.currentTier !== 'Free' || !data.billingEnabled) return null

  return (
    <NavLink
      to="/app/settings?section=subscription"
      className="group relative mx-3 mb-3 block overflow-hidden rounded-xl border border-primary-200 bg-gradient-to-br from-primary-50 to-white p-3.5 dark:border-primary-800 dark:from-primary-900/20 dark:to-slate-900"
    >
      <span className="absolute -right-6 -top-6 h-20 w-20 animate-[pulse-glow_8s_ease-in-out_infinite] rounded-full bg-primary-300/30 blur-2xl dark:bg-primary-700/20" />
      <div className="relative flex items-center gap-2 text-sm font-semibold text-primary-800 dark:text-primary-200">
        <Sparkles className="h-4 w-4" />
        Upgrade to Pro
      </div>
      <p className="relative mt-1 text-xs text-primary-700/80 dark:text-primary-300/70">
        Unlock advanced reports, more branches, and AI insights.
      </p>
      <span className="relative mt-2 inline-block text-xs font-medium text-primary-700 underline group-hover:no-underline dark:text-primary-300">
        See plans
      </span>
    </NavLink>
  )
}

export function Sidebar() {
  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r border-slate-200 bg-white lg:flex dark:border-slate-800 dark:bg-slate-900">
      <div className="flex h-16 items-center gap-2 px-5">
        <Logo className="h-8 w-8" />
        <span className="text-sm font-semibold text-slate-900 dark:text-slate-100">The Shop Keeper</span>
      </div>

      <nav className="flex-1 space-y-0.5 overflow-y-auto px-3 py-2">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/app'}
            data-tour={item.tourId}
            className={({ isActive }) =>
              clsx(
                'relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-all duration-200',
                isActive
                  ? 'bg-primary-50 text-primary-700 dark:bg-primary-900/30 dark:text-primary-300'
                  : 'text-slate-600 hover:translate-x-0.5 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800',
              )
            }
          >
            {({ isActive }) => (
              <>
                <span
                  className={clsx(
                    'absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-primary-600 transition-all duration-200',
                    isActive ? 'scale-y-100 opacity-100' : 'scale-y-0 opacity-0',
                  )}
                />
                <item.icon className="h-[18px] w-[18px]" />
                {item.label}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <UpgradeToProCard />
    </aside>
  )
}
