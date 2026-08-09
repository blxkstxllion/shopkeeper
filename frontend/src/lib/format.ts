// Hardcoded to GHS for now - the business's currencyCode isn't in the session/JWT yet,
// only in the one-time onboarding response. Swap for a real per-business lookup once
// currency is threaded through auth state.
export function formatMoney(amount: number): string {
  return `GH₵${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' })
}
