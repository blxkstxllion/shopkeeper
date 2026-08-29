import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Sparkles, ChevronDown, LogOut, Building2, Sun, Moon, Info, Compass } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { useBranchContext } from '@/contexts/BranchContext'
import { useTheme } from '@/contexts/ThemeContext'
import { useTour } from '@/features/tour/TourContext'
import { Avatar } from '@/components/ui/Avatar'
import { NotificationBell } from './NotificationBell'
import { OfflineStatusIndicator } from '@/offline/OfflineStatusIndicator'

export function TopNav() {
  const { user, activeBusiness, logout } = useAuth()
  const { branches, activeBranch, setActiveBranchId, canSwitchBranches } = useBranchContext()
  const { theme, toggleTheme } = useTheme()
  const { start: startTour } = useTour()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)
  const [branchMenuOpen, setBranchMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const branchMenuRef = useRef<HTMLDivElement>(null)

  const branchTriggerRef = useRef<HTMLButtonElement>(null)
  const accountTriggerRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false)
      if (branchMenuRef.current && !branchMenuRef.current.contains(e.target as Node)) setBranchMenuOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  useEffect(() => {
    if (!menuOpen && !branchMenuOpen) return
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key !== 'Escape') return
      if (menuOpen) {
        setMenuOpen(false)
        accountTriggerRef.current?.focus()
      }
      if (branchMenuOpen) {
        setBranchMenuOpen(false)
        branchTriggerRef.current?.focus()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [menuOpen, branchMenuOpen])

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <header className="flex h-16 items-center gap-4 border-b border-slate-200 bg-white px-4 lg:px-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex min-w-0 items-center gap-2">
        <Building2 className="h-4 w-4 shrink-0 text-slate-400" />
        <span className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">
          {activeBusiness?.businessName ?? '—'}
        </span>
        {activeBranch && (
          <>
            <span className="text-slate-300 dark:text-slate-600">/</span>
            {canSwitchBranches ? (
              <div className="relative" ref={branchMenuRef}>
                <button
                  ref={branchTriggerRef}
                  type="button"
                  onClick={() => setBranchMenuOpen((o) => !o)}
                  aria-haspopup="true"
                  aria-expanded={branchMenuOpen}
                  className="flex items-center gap-1 truncate rounded-md px-1.5 py-0.5 text-sm text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
                >
                  {activeBranch.name}
                  <ChevronDown className="h-3 w-3 shrink-0" />
                </button>
                {branchMenuOpen && (
                  <div className="absolute left-0 top-full z-10 mt-1 w-52 rounded-lg border border-slate-200 bg-white py-1 shadow-lg dark:border-slate-700 dark:bg-slate-900">
                    {branches.map((b) => (
                      <button
                        key={b.id}
                        type="button"
                        onClick={() => {
                          setActiveBranchId(b.id)
                          setBranchMenuOpen(false)
                        }}
                        className={`flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-800 ${
                          b.id === activeBranch.id
                            ? 'font-medium text-primary-700 dark:text-primary-400'
                            : 'text-slate-700 dark:text-slate-300'
                        }`}
                      >
                        {b.name}
                        {b.isMainBranch && <span className="text-xs text-slate-400">Main</span>}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <span className="truncate text-sm text-slate-500 dark:text-slate-400">{activeBranch.name}</span>
            )}
          </>
        )}
      </div>

      <div className="relative hidden flex-1 max-w-md sm:block">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          type="search"
          placeholder="Search products, sales, customers…"
          aria-label="Search products, sales, customers"
          className="h-9 w-full rounded-lg border border-slate-200 bg-slate-50 pl-9 pr-3 text-sm text-slate-700 placeholder:text-slate-400 focus:border-primary-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary-500/30 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:focus:bg-slate-900"
        />
      </div>

      <div className="ml-auto flex items-center gap-1">
        <OfflineStatusIndicator />
        <button
          className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
          aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
          onClick={toggleTheme}
        >
          {theme === 'dark' ? <Sun className="h-[18px] w-[18px]" /> : <Moon className="h-[18px] w-[18px]" />}
        </button>
        <button
          className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
          aria-label="AI Advisor"
          onClick={() => navigate('/app/ai')}
        >
          <Sparkles className="h-[18px] w-[18px]" />
        </button>
        <NotificationBell />

        <div className="relative ml-1" ref={menuRef}>
          <button
            ref={accountTriggerRef}
            type="button"
            onClick={() => setMenuOpen((o) => !o)}
            aria-haspopup="true"
            aria-expanded={menuOpen}
            aria-label="Account menu"
            className="flex items-center gap-2 rounded-lg py-1.5 pl-1.5 pr-2 hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <Avatar firstName={user?.firstName} lastName={user?.lastName} photoUrl={user?.photoUrl} size="sm" />
            <ChevronDown className="h-3.5 w-3.5 text-slate-400" />
          </button>

          {menuOpen && (
            <div
              className="fixed inset-x-4 top-16 z-20 rounded-lg border border-slate-200 bg-white py-1 shadow-lg
                sm:absolute sm:inset-x-auto sm:right-0 sm:top-full sm:mt-1 sm:w-56
                dark:border-slate-700 dark:bg-slate-900"
            >
              <div className="border-b border-slate-100 px-3 py-2 dark:border-slate-800">
                <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="truncate text-xs text-slate-500 dark:text-slate-400">{user?.email}</p>
              </div>
              <button
                type="button"
                onClick={() => {
                  setMenuOpen(false)
                  navigate('/app/about')
                }}
                className="flex w-full items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <Info className="h-4 w-4" />
                About
              </button>
              <button
                type="button"
                onClick={() => {
                  setMenuOpen(false)
                  startTour()
                }}
                className="flex w-full items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <Compass className="h-4 w-4" />
                Take the tour
              </button>
              <button
                type="button"
                onClick={handleLogout}
                className="flex w-full items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <LogOut className="h-4 w-4" />
                Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
