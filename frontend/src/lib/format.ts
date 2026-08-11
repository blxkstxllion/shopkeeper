// Hardcoded to GHS for now - the business's currencyCode isn't in the session/JWT yet,
// only in the one-time onboarding response. Swap for a real per-business lookup once
// currency is threaded through auth state.
export function formatMoney(amount: number): string {
  return `GH₵${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' })
}

/**
 * Product/upload image URLs come back from the API as origin-relative paths
 * (e.g. "/uploads/products/xyz.jpg"), not full URLs - resolve against the API's
 * origin (VITE_API_BASE_URL minus its trailing "/api") so <img src> works directly.
 */
export function resolveUploadUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path
  const apiOrigin = import.meta.env.VITE_API_BASE_URL.replace(/\/api\/?$/, '')
  return `${apiOrigin}${path}`
}
