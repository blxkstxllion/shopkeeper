import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/types/product'
import type { CreateSalePayload, Refund, Sale, SaleListItem, SellableProduct } from '@/types/sale'

export async function getSellableProducts(branchId: string, search?: string, categoryId?: string): Promise<SellableProduct[]> {
  const { data } = await apiClient.get<SellableProduct[]>('/sales/sellable-products', { params: { branchId, search, categoryId } })
  return data
}

export async function createSale(payload: CreateSalePayload): Promise<Sale> {
  const { data } = await apiClient.post<Sale>('/sales', payload)
  return data
}

export async function getSale(id: string): Promise<Sale> {
  const { data } = await apiClient.get<Sale>(`/sales/${id}`)
  return data
}

export async function getSales(params: {
  branchId?: string
  from?: string
  to?: string
  status?: string
  page?: number
  pageSize?: number
}): Promise<PagedResult<SaleListItem>> {
  const { data } = await apiClient.get<PagedResult<SaleListItem>>('/sales', { params })
  return data
}

export async function voidSale(id: string, reason: string): Promise<void> {
  await apiClient.post(`/sales/${id}/void`, { reason })
}

export async function refundSale(
  id: string,
  payload: { items: { saleItemId: string; quantity: number }[]; reason: string },
): Promise<Refund> {
  const { data } = await apiClient.post<Refund>(`/sales/${id}/refund`, payload)
  return data
}
