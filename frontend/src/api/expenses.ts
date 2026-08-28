import { apiClient } from '@/lib/api-client'
import type { CreateExpensePayload, Expense, ExpenseCategory, PagedResult, UpdateExpensePayload } from '@/types/expense'

export interface GetExpensesParams {
  from?: string
  to?: string
  categoryId?: string
  branchId?: string
  page?: number
  pageSize?: number
}

export async function getExpenses(params: GetExpensesParams): Promise<PagedResult<Expense>> {
  const { data } = await apiClient.get<PagedResult<Expense>>('/expenses', { params })
  return data
}

export async function createExpense(payload: CreateExpensePayload): Promise<Expense> {
  const { data } = await apiClient.post<Expense>('/expenses', payload)
  return data
}

export async function updateExpense(payload: UpdateExpensePayload): Promise<void> {
  await apiClient.put(`/expenses/${payload.id}`, payload)
}

export async function deleteExpense(id: string): Promise<void> {
  await apiClient.delete(`/expenses/${id}`)
}

export async function getExpenseCategories(): Promise<ExpenseCategory[]> {
  const { data } = await apiClient.get<ExpenseCategory[]>('/expenses/categories')
  return data
}

export async function createExpenseCategory(payload: {
  name: string
  description?: string | null
}): Promise<ExpenseCategory> {
  const { data } = await apiClient.post<ExpenseCategory>('/expenses/categories', payload)
  return data
}
