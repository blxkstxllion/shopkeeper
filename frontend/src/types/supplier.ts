export interface Supplier {
  id: string
  name: string
  contactName: string | null
  phone: string | null
  email: string | null
  address: string | null
  isActive: boolean
}

export interface SupplierRestock {
  id: string
  productId: string
  productName: string
  branchId: string
  branchName: string
  quantity: number
  createdByName: string
  createdAt: string
}

export interface CreateSupplierPayload {
  name: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
  address?: string | null
}

export type UpdateSupplierPayload = CreateSupplierPayload & { id: string; isActive: boolean }

export interface RestockFromSupplierPayload {
  productId: string
  branchId: string
  quantity: number
  unitCost?: number | null
}
