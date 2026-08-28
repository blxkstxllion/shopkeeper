// Module-level rather than threaded through every formatMoney call site (there are ~50 across
// the app) - there's only ever one "active" currency per session, so AuthContext updates this
// once whenever activeBusiness changes (see its useEffect), and every call site stays unchanged.
let activeCurrencyCode = 'GHS'

export function setActiveCurrencyCode(code: string): void {
  activeCurrencyCode = code
}

// Locale drives which currency symbol Intl picks (its en-US data has no dedicated symbol for
// most African currencies - GHS falls back to the bare ISO code "GHS" instead of "GH₵" - so a
// single fixed locale can't serve every currency this app's onboarding now offers). Mapped to
// the currency's own home-country English locale, which reliably has its real symbol.
const CURRENCY_LOCALES: Record<string, string> = {
  GHS: 'en-GH',
  NGN: 'en-NG',
  KES: 'en-KE',
  ZAR: 'en-ZA',
  UGX: 'en-UG',
  TZS: 'en-TZ',
  RWF: 'rw-RW',
  ZMW: 'en-ZM',
  MWK: 'en-MW',
  BWP: 'en-BW',
  NAD: 'en-NA',
  MZN: 'pt-MZ',
  ETB: 'en-ET',
  EGP: 'ar-EG',
  MAD: 'ar-MA',
  XOF: 'fr-SN',
  XAF: 'fr-CM',
  CDF: 'fr-CD',
  SLL: 'en-SL',
  LRD: 'en-LR',
  AOA: 'pt-AO',
  MUR: 'en-MU',
  GBP: 'en-GB',
  USD: 'en-US',
  EUR: 'en-IE',
  CAD: 'en-CA',
  AED: 'ar-AE',
  INR: 'en-IN',
  CNY: 'zh-CN',
  AUD: 'en-AU',
}

export function formatMoney(amount: number): string {
  try {
    return new Intl.NumberFormat(CURRENCY_LOCALES[activeCurrencyCode] ?? 'en-US', {
      style: 'currency',
      currency: activeCurrencyCode,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount)
  } catch {
    // Intl throws on a currency code it doesn't recognize - onboarding now restricts this to a
    // fixed dropdown, but older businesses or direct API use could still carry a stale/invalid
    // code, so fall back rather than crash the whole page.
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
