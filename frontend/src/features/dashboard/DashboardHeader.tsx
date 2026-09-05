import { useRef, useState, type ChangeEvent } from 'react'
import { Link } from 'react-router-dom'
import { Camera, Loader2, ShoppingCart, PackagePlus, BarChart3, type LucideIcon } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { uploadProfilePhoto, updateProfilePhoto } from '@/api/auth'
import { Avatar } from '@/components/ui/Avatar'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

// Two very subtle, slow-pulsing glow blobs - deliberately far more restrained than the
// login page's ambience (this is a working dashboard, not a marketing surface). No
// particles, no floating shapes here - motion should read as "alive", not "busy".
function HeroAmbience() {
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden rounded-2xl">
      <div className="absolute -right-10 -top-16 h-48 w-48 animate-[pulse-glow_9s_ease-in-out_infinite] rounded-full bg-primary-300/30 blur-3xl dark:bg-primary-700/20" />
      <div
        className="absolute -bottom-16 left-1/3 h-40 w-40 animate-[pulse-glow_11s_ease-in-out_infinite] rounded-full bg-primary-200/30 blur-3xl dark:bg-primary-800/15"
        style={{ animationDelay: '3s' }}
      />
    </div>
  )
}

function QuickAction({
  to,
  icon: Icon,
  label,
  primary,
}: {
  to: string
  icon: LucideIcon
  label: string
  primary?: boolean
}) {
  return (
    <Link
      to={to}
      className={
        primary
          ? 'group inline-flex items-center gap-1.5 rounded-lg bg-primary-600 px-3.5 py-2 text-sm font-medium text-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-primary-700 hover:shadow-md active:translate-y-0'
          : 'group inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white/80 px-3.5 py-2 text-sm font-medium text-slate-700 backdrop-blur-sm transition-all duration-200 hover:-translate-y-0.5 hover:bg-white hover:shadow-md active:translate-y-0 dark:border-slate-700 dark:bg-slate-900/60 dark:text-slate-200 dark:hover:bg-slate-900'
      }
    >
      <Icon className="h-4 w-4" />
      {label}
    </Link>
  )
}

/** Themed identity + hero banner at the top of the Dashboard - photo, name, role, shop name,
 * and the day's quick actions. The avatar here is the one entry point for changing a profile
 * photo (click -> file picker -> upload -> save -> refresh) - TopNav's avatar stays read-only
 * to avoid a second, redundant edit affordance for the same action. */
export function DashboardHeader() {
  const { user, activeBusiness, refreshUser } = useAuth()
  const isOnline = useOnlineStatus()
  const [isUploading, setIsUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function handleFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-selecting the same file later
    if (!file) return

    if (!isOnline) {
      setError("Changing your photo needs internet - you can try again once you're back online.")
      return
    }

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      setError('Please choose a JPEG, PNG, WEBP, or GIF image.')
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setError('Images must be 5MB or smaller.')
      return
    }

    setError(null)
    setIsUploading(true)
    try {
      const { url } = await uploadProfilePhoto(file)
      await updateProfilePhoto(url)
      await refreshUser()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to update your photo. Please try again.')
    } finally {
      setIsUploading(false)
    }
  }

  return (
    <div className="mb-6 flex flex-col gap-2">
      <div className="relative overflow-hidden rounded-2xl border border-primary-200 bg-gradient-to-br from-primary-50 via-white to-primary-50/40 p-4 dark:border-primary-800 dark:from-primary-900/20 dark:via-slate-900 dark:to-primary-900/5 sm:p-5">
        <HeroAmbience />
        <div className="relative flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={isUploading || !isOnline}
              aria-label="Change profile photo"
              className="group relative shrink-0 rounded-full focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-600 disabled:cursor-not-allowed"
            >
              <Avatar firstName={user?.firstName} lastName={user?.lastName} photoUrl={user?.photoUrl} size="lg" />
              <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/0 text-white opacity-0 transition-all group-hover:bg-black/40 group-hover:opacity-100">
                {isUploading ? <Loader2 className="h-5 w-5 animate-spin" /> : <Camera className="h-5 w-5" />}
              </span>
              <input
                ref={fileInputRef}
                type="file"
                aria-label="Upload profile photo"
                accept={ALLOWED_IMAGE_TYPES.join(',')}
                onChange={handleFileSelected}
                className="hidden"
              />
            </button>
            <div className="min-w-0">
              <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">
                Welcome back, {user?.firstName} <span aria-hidden="true">👋</span>
              </p>
              <p className="truncate text-sm text-slate-600 dark:text-slate-400">
                Here&apos;s what&apos;s happening at {activeBusiness?.businessName} today.
              </p>
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <QuickAction to="/app/sell" icon={ShoppingCart} label="New sale" primary />
            <QuickAction to="/app/inventory" icon={PackagePlus} label="Add product" />
            <QuickAction to="/app/reports" icon={BarChart3} label="View reports" />
          </div>
        </div>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
    </div>
  )
}
