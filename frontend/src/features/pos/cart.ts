import type { SellableProduct } from '@/types/sale'

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
