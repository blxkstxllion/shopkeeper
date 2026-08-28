import { apiClient } from '@/lib/api-client'
import type {
  CreateSupplierPayload,
  RestockFromSupplierPayload,
  Supplier,
  SupplierRestock,
  UpdateSupplierPayload,
} from '@/types/supplier'

export async function getSuppliers(): Promise<Supplier[]> {
  const { data } = await apiClient.get<Supplier[]>('/suppliers')
  return data
}

export async function createSupplier(payload: CreateSupplierPayload): Promise<Supplier> {
  const { data } = await apiClient.post<Supplier>('/suppliers', payload)
  return data
}

export async function updateSupplier(payload: UpdateSupplierPayload): Promise<void> {
  await apiClient.put(`/suppliers/${payload.id}`, payload)
}

export async function deleteSupplier(id: string): Promise<void> {
  await apiClient.delete(`/suppliers/${id}`)
}

export async function getSupplierRestockHistory(supplierId: string): Promise<SupplierRestock[]> {
  const { data } = await apiClient.get<SupplierRestock[]>(`/suppliers/${supplierId}/restocks`)
  return data
}

export async function restockFromSupplier(
  supplierId: string,
  payload: RestockFromSupplierPayload,
): Promise<{ quantityOnHand: number }> {
  const { data } = await apiClient.post<{ quantityOnHand: number }>(`/suppliers/${supplierId}/restock`, {
    supplierId,
    ...payload,
  })
  return data
}
