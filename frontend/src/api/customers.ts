import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/types/product'
import type {
  CreateCustomerPayload,
  Customer,
  CustomerDetail,
  CustomerLedgerEntry,
  RecordCustomerPaymentPayload,
  UpdateCustomerPayload,
} from '@/types/customer'

export interface GetCustomersParams {
  search?: string
  activeOnly?: boolean
  page?: number
  pageSize?: number
}

export async function getCustomers(params: GetCustomersParams): Promise<PagedResult<Customer>> {
  const { data } = await apiClient.get<PagedResult<Customer>>('/customers', { params })
  return data
}

export async function getCustomer(id: string): Promise<CustomerDetail> {
  const { data } = await apiClient.get<CustomerDetail>(`/customers/${id}`)
  return data
}

export async function createCustomer(payload: CreateCustomerPayload & { clientRequestId?: string }): Promise<Customer> {
  const { data } = await apiClient.post<Customer>('/customers', payload)
  return data
}

export async function updateCustomer(payload: UpdateCustomerPayload & { clientRequestId?: string }): Promise<void> {
  await apiClient.put(`/customers/${payload.id}`, payload)
}

export async function deleteCustomer(id: string, clientRequestId?: string): Promise<void> {
  await apiClient.delete(`/customers/${id}`, { params: { clientRequestId } })
}

export async function recordCustomerPayment(
  customerId: string,
  payload: RecordCustomerPaymentPayload,
): Promise<CustomerLedgerEntry> {
  const { data } = await apiClient.post<CustomerLedgerEntry>(`/customers/${customerId}/payments`, payload)
  return data
}

export async function getCustomerLedger(
  customerId: string,
  params?: { page?: number; pageSize?: number },
): Promise<PagedResult<CustomerLedgerEntry>> {
  const { data } = await apiClient.get<PagedResult<CustomerLedgerEntry>>(`/customers/${customerId}/ledger`, { params })
  return data
}
