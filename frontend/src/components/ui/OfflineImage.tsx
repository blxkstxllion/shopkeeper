import type { ReactNode } from 'react'
import { useOfflineImage } from '@/offline/useOfflineImage'

/** Drop-in replacement for `<img src={resolveUploadUrl(rawUrl)} />` wherever an image is
 * DISPLAYED (not uploaded) - resolves to the real URL online, or a locally cached copy
 * offline if one was downloaded during eager sync (see offline/imageCache.ts). Renders
 * `fallback` (the placeholder icon every call site already had) whenever there's no image at
 * all, or offline with nothing cached for it. */
export function OfflineImage({
  src,
  alt,
  className,
  fallback,
}: {
  src: string | null | undefined
  alt: string
  className?: string
  fallback: ReactNode
}) {
  const resolvedSrc = useOfflineImage(src)
  if (!resolvedSrc) return <>{fallback}</>
  return <img src={resolvedSrc} alt={alt} className={className} />
}
