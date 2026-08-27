import { resolveUploadUrl } from '@/lib/format'

type Size = 'sm' | 'lg'

const sizeClasses: Record<Size, string> = {
  sm: 'h-7 w-7 text-xs',
  lg: 'h-14 w-14 text-lg',
}

/** Photo if the user has one, else the same colored-initials circle used across the app -
 * already theme-aware via bg-primary-100/text-primary-700, no per-theme logic needed here. */
export function Avatar({
  firstName,
  lastName,
  photoUrl,
  size = 'sm',
  className,
}: {
  firstName?: string
  lastName?: string
  photoUrl?: string | null
  size?: Size
  className?: string
}) {
  const sizeClass = sizeClasses[size]

  if (photoUrl) {
    return (
      <img
        src={resolveUploadUrl(photoUrl)}
        alt=""
        className={`${sizeClass} shrink-0 rounded-full object-cover ${className ?? ''}`}
      />
    )
  }

  return (
    <div
      className={`flex ${sizeClass} shrink-0 items-center justify-center rounded-full bg-primary-100 font-semibold text-primary-700 dark:bg-primary-900/40 dark:text-primary-300 ${className ?? ''}`}
    >
      {firstName?.[0]}
      {lastName?.[0]}
    </div>
  )
}
