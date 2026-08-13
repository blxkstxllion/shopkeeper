export interface ProductCategory {
  id: string
  name: string
  description: string | null
  isActive: boolean
}

export interface Product {
  id: string
  name: string
  sku: string
  barcode: string | null
  description: string | null
  imageUrl: string | null
  categoryId: string | null
  categoryName: string | null
  supplierId: string | null
  supplierName: string | null
  sellingPrice: number
  costPrice: number
  minStock: number
  reorderLevel: number
  trackInventory: boolean
  isActive: boolean
  quantityOnHand: number | null
  isLowStock: boolean
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateProductPayload {
  name: string
  sku: string
  barcode?: string | null
  description?: string | null
  imageUrl?: string | null
  categoryId?: string | null
  supplierId?: string | null
  sellingPrice: number
  costPrice: number
  minStock: number
  reorderLevel: number
  trackInventory: boolean
  initialQuantity: number
  branchId?: string | null
}

export type UpdateProductPayload = CreateProductPayload & { id: string; isActive: boolean }

export interface InventoryTransaction {
  id: string
  productId: string
  productName: string
  branchId: string
  branchName: string
  type: 'InitialStock' | 'Adjustment' | 'Sale' | 'Refund'
  quantityChange: number
  quantityAfter: number
  reason: string
  referenceType: string | null
  referenceId: string | null
  createdByName: string
  createdAt: string
}
