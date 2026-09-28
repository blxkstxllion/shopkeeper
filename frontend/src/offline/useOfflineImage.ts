import { useEffect, useState } from 'react'
import { useAuth } from '@/contexts/AuthContext'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'
import { resolveUploadUrl } from '@/lib/format'
import { getCachedImageBlobUrl } from './imageCache'

/** Resolves an image (origin-relative path from the API, e.g. product photo or business
 * logo) to something an <img> tag can actually load right now. Online, that's just the real
 * URL - fast and always fresh, no reason to touch the offline cache at all. Offline, it looks
 * up whatever was downloaded during eager sync and returns a local object URL instead; if
 * nothing was ever cached (never synced, or synced before this image existed), returns null
 * and the caller falls back to its usual placeholder icon. */
export function useOfflineImage(rawUrl: string | null | undefined): string | null {
  const { activeBusiness } = useAuth()
  const businessId = activeBusiness?.businessId
  const isOnline = useOnlineStatus()
  const [offlineUrl, setOfflineUrl] = useState<string | null>(null)

  useEffect(() => {
    setOfflineUrl(null)
    if (isOnline || !rawUrl || !businessId) return

    let cancelled = false
    let created: string | null = null
    getCachedImageBlobUrl(resolveUploadUrl(rawUrl), businessId).then((blobUrl) => {
      if (cancelled) {
        if (blobUrl) URL.revokeObjectURL(blobUrl)
        return
      }
      created = blobUrl
      setOfflineUrl(blobUrl)
    })

    return () => {
      cancelled = true
      if (created) URL.revokeObjectURL(created)
    }
  }, [rawUrl, businessId, isOnline])

  if (!rawUrl) return null
  return isOnline ? resolveUploadUrl(rawUrl) : offlineUrl
}
