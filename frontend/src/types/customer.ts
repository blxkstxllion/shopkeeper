export interface Customer {
  id: string
  name: string
  phone: string | null
  email: string | null
  address: string | null
  isActive: boolean
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
