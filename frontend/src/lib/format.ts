// Module-level rather than threaded through every formatMoney call site (there are ~50 across
// the app) - there's only ever one "active" currency per session, so AuthContext updates this
// once whenever activeBusiness changes (see its useEffect), and every call site stays unchanged.
let activeCurrencyCode = 'GHS'

export function setActiveCurrencyCode(code: string): void {
  activeCurrencyCode = code
}

export function formatMoney(amount: number): string {
  try {
    // en-GH, not en-US: this app's primary/default currency is GHS, and Intl's en-US locale
    // data has no dedicated GH₵ symbol for it (falls back to the bare "GHS" ISO code) - en-GH
    // preserves the exact symbol this formatter always showed, while still handling any other
    // currencyCode a business sets reasonably (e.g. USD renders "US$", unambiguous either way).
    return new Intl.NumberFormat('en-GH', {
      style: 'currency',
      currency: activeCurrencyCode,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount)
  } catch {
    // Intl throws on a currency code Intl doesn't recognize (currencyCode is free text at
    // onboarding, e.g. a typo) - fall back rather than crash the whole page.
    return `${activeCurrencyCode} ${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
  }
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
