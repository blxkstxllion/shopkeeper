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
