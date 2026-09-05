import { apiClient } from '@/lib/api-client'
import type { InventoryTransaction, PagedResult } from '@/types/product'

export interface AdjustStockPayload {
  productId: string
  branchId: string
  quantityChange: number
  reason: string
  clientRequestId?: string
}

export async function adjustStock(payload: AdjustStockPayload): Promise<{ quantityOnHand: number }> {
  const { data } = await apiClient.post<{ quantityOnHand: number }>('/inventory/adjust', payload)
  return data
}

export async function getInventoryTransactions(params: {
  productId?: string
  branchId?: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<InventoryTransaction>> {
  const { data } = await apiClient.get<PagedResult<InventoryTransaction>>('/inventory/transactions', { params })
  return data
}

export interface InventoryStats {
  totalProducts: number
  lowStockCount: number
  outOfStockCount: number
  inventoryValue: number
}

export async function getInventoryStats(branchId?: string): Promise<InventoryStats> {
  const { data } = await apiClient.get<InventoryStats>('/inventory/stats', { params: { branchId } })
  return data
}
