import { getOfflineDb } from './db'

/** Downloads an image and stores the actual bytes locally, keyed by its fully-resolved URL -
 * used by eager sync (business logo, product photos) so they render offline, not just their
 * web address. Best-effort: a failed download (slow connection, image deleted server-side)
 * silently skips rather than failing the whole eager-sync pass it's part of. */
export async function cacheImageBlob(resolvedUrl: string, businessId: string): Promise<void> {
  try {
    const response = await fetch(resolvedUrl)
    if (!response.ok) return
    const blob = await response.blob()
    const db = await getOfflineDb(businessId)
    await db.put('imageBlobs', { url: resolvedUrl, blob, cachedAt: new Date().toISOString() })
  } catch {
    // Network error, CORS, or anything else - nothing to do, this image just won't be
    // available offline. Not worth surfacing since eager sync itself is a silent warm-up.
  }
}

/** Returns a local, offline-usable object URL for a previously cached image, or null if none
 * was ever downloaded. Callers own the returned URL's lifetime and must call
 * URL.revokeObjectURL on it when done (see useOfflineImage). */
export async function getCachedImageBlobUrl(resolvedUrl: string, businessId: string): Promise<string | null> {
  const db = await getOfflineDb(businessId)
  const record = await db.get('imageBlobs', resolvedUrl)
  if (!record) return null
  return URL.createObjectURL(record.blob)
}
