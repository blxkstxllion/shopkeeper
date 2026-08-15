export type PaymentMethod = 'Cash' | 'Card' | 'MobileMoney'

export type SaleStatus = 'Completed' | 'Voided' | 'PartiallyRefunded' | 'Refunded'

export interface SellableProduct {
  productId: string
  name: string
  sku: string
  barcode: string | null
  imageUrl: string | null
  categoryId: string | null
  sellingPrice: number
  trackInventory: boolean
  quantityOnHand: number | null
}

export interface SaleItem {
  id: string
  productId: string
  productName: string
  sku: string
  quantity: number
  unitPrice: number
  unitCost: number
  discountAmount: number
  lineRevenue: number
  lineCost: number
  lineProfit: number
  refundedQuantity: number
}

export interface Payment {
  id: string
  method: PaymentMethod
  amount: number
  referenceNumber: string | null
}

export interface Sale {
  id: string
  saleNumber: string
  branchId: string
  branchName: string
  customerId: string | null
  customerName: string | null
  cashierUserId: string
  cashierName: string
  subtotal: number
  discountAmount: number
  taxAmount: number
  total: number
  totalCost: number
  grossProfit: number
  status: SaleStatus
  voidedAt: string | null
  voidReason: string | null
  createdAt: string
  items: SaleItem[]
  payments: Payment[]
}

export interface SaleListItem {
  id: string
  saleNumber: string
  branchName: string
  cashierName: string
  total: number
  grossProfit: number
  status: SaleStatus
  itemCount: number
  createdAt: string
}

export interface SaleLineInput {
  productId: string
  quantity: number
  discountAmount: number
}

export interface SalePaymentInput {
  method: PaymentMethod
  amount: number
  referenceNumber?: string | null
}

export interface CreateSalePayload {
  branchId: string
  items: SaleLineInput[]
  discountAmount: number
  payments: SalePaymentInput[]
  customerId?: string | null
  /** Client-generated idempotency key, sent on every attempt (online or queued offline)
   * so a retry - whether from a flaky connection or a resync after reconnecting - can
   * never double-sell. See CreateSaleCommandHandler's use of it on the backend. */
  clientRequestId: string
}

/** A sale that couldn't reach the server (offline, or the request failed with a real
 * network error) and is sitting in the outbox waiting to sync. It has no server-assigned
 * id/saleNumber yet, and no cost/profit figures (SellableProduct doesn't carry cost) -
 * everything shown here is derived purely from the cart at the moment it was queued. */
export interface QueuedSale {
  queued: true
  clientRequestId: string
  branchName: string
  queuedAt: string
  items: { productId: string; productName: string; quantity: number; unitPrice: number; lineRevenue: number }[]
  subtotal: number
  discountAmount: number
  total: number
  payments: { method: PaymentMethod; amount: number }[]
}

export interface Refund {
  id: string
  refundNumber: string
  saleId: string
  saleNumber: string
  reason: string
  totalAmount: number
  createdAt: string
}
