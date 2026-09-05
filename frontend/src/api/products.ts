import { apiClient } from '@/lib/api-client'
import type { CreateProductPayload, PagedResult, Product, ProductCategory, UpdateProductPayload } from '@/types/product'

export interface GetProductsParams {
  search?: string
  categoryId?: string
  branchId?: string
  activeOnly?: boolean
  lowStockOnly?: boolean
  page?: number
  pageSize?: number
}

export async function getProducts(params: GetProductsParams): Promise<PagedResult<Product>> {
  const { data } = await apiClient.get<PagedResult<Product>>('/products', { params })
  return data
}

export async function getProduct(id: string, branchId?: string): Promise<Product> {
  const { data } = await apiClient.get<Product>(`/products/${id}`, { params: { branchId } })
  return data
}

export async function createProduct(payload: CreateProductPayload & { clientRequestId?: string }): Promise<Product> {
  const { data } = await apiClient.post<Product>('/products', payload)
  return data
}

export async function updateProduct(payload: UpdateProductPayload & { clientRequestId?: string }): Promise<void> {
  await apiClient.put(`/products/${payload.id}`, payload)
}

export async function deleteProduct(id: string, clientRequestId?: string): Promise<void> {
  await apiClient.delete(`/products/${id}`, { params: { clientRequestId } })
}

export async function getProductCategories(): Promise<ProductCategory[]> {
  const { data } = await apiClient.get<ProductCategory[]>('/products/categories')
  return data
}

export async function createProductCategory(payload: {
  name: string
  description?: string | null
  clientRequestId?: string
}): Promise<ProductCategory> {
  const { data } = await apiClient.post<ProductCategory>('/products/categories', payload)
  return data
}

export async function uploadProductImage(file: File | Blob, clientRequestId?: string): Promise<{ url: string }> {
  const formData = new FormData()
  formData.append('file', file)
  if (clientRequestId) formData.append('clientRequestId', clientRequestId)
  const { data } = await apiClient.post<{ url: string }>('/uploads/product-image', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}
