export interface ExpenseCategory {
  id: string
  name: string
  description: string | null
  isActive: boolean
}

export interface Expense {
  id: string
  branchId: string | null
  branchName: string | null
  expenseCategoryId: string
  categoryName: string
  amount: number
  expenseDate: string
  description: string | null
  createdByName: string
  createdAt: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface CreateExpensePayload {
  branchId?: string | null
  expenseCategoryId: string
  amount: number
  expenseDate: string
  description?: string | null
}

export type UpdateExpensePayload = CreateExpensePayload & { id: string }
