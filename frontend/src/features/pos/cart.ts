import type { SellableProduct } from '@/types/sale'
import type { BusinessSettings } from '@/types/businessSettings'

export interface CartLine {
  product: SellableProduct
  quantity: number
  discountAmount: number
}

export function cartSubtotal(lines: CartLine[]): number {
  return lines.reduce((sum, l) => sum + l.product.sellingPrice * l.quantity, 0)
}

export function cartLineDiscountTotal(lines: CartLine[]): number {
  return lines.reduce((sum, l) => sum + l.discountAmount, 0)
}

/** Mirrors CreateSaleCommand's tax math exactly (backend/.../CreateSaleCommand.cs) - the backend
 * is the source of truth and rejects any payment total that doesn't match its own calculation,
 * so a divergent frontend formula here isn't a rounding quirk, it's checkout being unusable for
 * any tax-exclusive-pricing business (every sale would hit that rejection). No business settings
 * loaded yet (not cached offline, first-ever open) falls back to no tax, same as the backend's
 * own null-BusinessSetting fallback. */
export function calculateSaleTotal(
  taxableAmount: number,
  settings: Pick<BusinessSettings, 'taxEnabled' | 'taxRatePercent' | 'taxInclusivePricing'> | undefined,
): { taxAmount: number; total: number } {
  if (!settings?.taxEnabled || settings.taxRatePercent <= 0) {
    return { taxAmount: 0, total: taxableAmount }
  }
  const rate = settings.taxRatePercent / 100
  if (settings.taxInclusivePricing) {
    return { taxAmount: Math.round(((taxableAmount * rate) / (1 + rate)) * 100) / 100, total: taxableAmount }
  }
  const taxAmount = Math.round(taxableAmount * rate * 100) / 100
  return { taxAmount, total: taxableAmount + taxAmount }
}
