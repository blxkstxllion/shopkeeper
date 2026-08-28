import { useRef, useState, type ChangeEvent } from 'react'
import { Camera, Loader2 } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { uploadProfilePhoto, updateProfilePhoto } from '@/api/auth'
import { Avatar } from '@/components/ui/Avatar'
import { Alert } from '@/components/ui/Alert'
import { ApiError } from '@/lib/api-client'

const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

/** Themed identity banner at the top of the Dashboard - photo, name, role, and shop name. The
 * avatar here is the one entry point for changing a profile photo (click -> file picker ->
 * upload -> save -> refresh) - TopNav's avatar stays read-only to avoid a second, redundant
 * edit affordance for the same action. */
export function DashboardHeader() {
  const { user, activeBusiness, refreshUser } = useAuth()
  const [isUploading, setIsUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function handleFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // allow re-selecting the same file later
    if (!file) return

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
      <div className="flex items-center gap-4 rounded-2xl border border-primary-200 bg-primary-50/60 p-4 dark:border-primary-800 dark:bg-primary-900/10">
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={isUploading}
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
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">Welcome back, {user?.firstName}</p>
          <p className="truncate text-sm text-slate-600 dark:text-slate-400">
            {activeBusiness?.roleName} at {activeBusiness?.businessName}
          </p>
        </div>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
    </div>
  )
}
