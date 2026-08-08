import { NavLink } from 'react-router-dom'
import { useState } from 'react'
import { MoreHorizontal, X } from 'lucide-react'
import { clsx } from 'clsx'
import { navItems } from '@/config/navigation'

export function MobileBottomNav() {
  const [moreOpen, setMoreOpen] = useState(false)
  const primary = navItems.filter((i) => i.mobilePriority)
  const rest = navItems.filter((i) => !i.mobilePriority)

  return (
    <>
      {moreOpen && (
        <div className="fixed inset-0 z-40 bg-black/30 lg:hidden" onClick={() => setMoreOpen(false)}>
          <div
            className="absolute bottom-16 left-0 right-0 rounded-t-2xl bg-white p-3 dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-2 flex items-center justify-between px-1">
              <span className="text-sm font-semibold text-slate-900 dark:text-slate-100">More</span>
              <button onClick={() => setMoreOpen(false)} aria-label="Close">
                <X className="h-4 w-4 text-slate-400" />
              </button>
            </div>
            <div className="grid grid-cols-3 gap-2">
              {rest.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  onClick={() => setMoreOpen(false)}
                  className="flex flex-col items-center gap-1.5 rounded-xl border border-slate-100 px-2 py-3 text-center dark:border-slate-800"
                >
                  <item.icon className="h-5 w-5 text-slate-600 dark:text-slate-300" />
                  <span className="text-xs text-slate-600 dark:text-slate-300">{item.label}</span>
                </NavLink>
              ))}
            </div>
          </div>
        </div>
      )}

      <nav className="fixed inset-x-0 bottom-0 z-30 flex h-16 items-stretch border-t border-slate-200 bg-white lg:hidden dark:border-slate-800 dark:bg-slate-900">
        {primary.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/app'}
            className={({ isActive }) =>
              clsx(
                'flex flex-1 flex-col items-center justify-center gap-0.5 text-xs font-medium',
                isActive ? 'text-primary-600 dark:text-primary-400' : 'text-slate-500 dark:text-slate-400',
              )
            }
          >
            <item.icon className="h-5 w-5" />
            {item.label}
          </NavLink>
        ))}
        <button
          onClick={() => setMoreOpen(true)}
          className="flex flex-1 flex-col items-center justify-center gap-0.5 text-xs font-medium text-slate-500 dark:text-slate-400"
        >
          <MoreHorizontal className="h-5 w-5" />
          More
        </button>
      </nav>
    </>
  )
}
