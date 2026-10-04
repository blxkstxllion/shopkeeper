export interface Customer {
  id: string
  name: string
  phone: string | null
  email: string | null
  address: string | null
  isActive: boolean
  /** Positive means the customer owes the business money. */
  currentBalance: number
  /** Concurrency token for currentBalance - send back on RefundSaleCommand's ApplyToBalance split. */
  balanceRowVersion: number
}

export interface CustomerDetail extends Customer {
  totalSpend: number
  averageSale: number
  purchaseCount: number
  lastPurchaseAt: string | null
}

export interface CreateCustomerPayload {
  name: string
  phone?: string | null
  email?: string | null
  address?: string | null
}

export type UpdateCustomerPayload = CreateCustomerPayload & { id: string; isActive: boolean }

export type CustomerLedgerEntryType = 'Charge' | 'Payment' | 'RefundCredit'

export interface CustomerLedgerEntry {
  id: string
  type: CustomerLedgerEntryType
  amount: number
  balanceAfter: number
  referenceType: string
  referenceId: string
  method: string | null
  referenceNumber: string | null
  note: string | null
  createdAt: string
}

export interface RecordCustomerPaymentPayload {
  amount: number
  method: 'Cash' | 'Card' | 'MobileMoney'
  referenceNumber?: string | null
  note?: string | null
  clientRequestId?: string
}
