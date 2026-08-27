import { apiClient } from '@/lib/api-client'
import type { ExpenseReport, InventoryReport, ProfitabilityReport } from '@/types/reports'

export interface ReportParams {
  from: string
  to: string
  branchId?: string
}

export async function getProfitabilityReport(params: ReportParams): Promise<ProfitabilityReport> {
  const { data } = await apiClient.get<ProfitabilityReport>('/reports/profitability', { params })
  return data
}

export async function getExpenseReport(params: ReportParams & { categoryId?: string }): Promise<ExpenseReport> {
  const { data } = await apiClient.get<ExpenseReport>('/reports/expenses', { params })
  return data
}

export async function getInventoryReport(params: ReportParams): Promise<InventoryReport> {
  const { data } = await apiClient.get<InventoryReport>('/reports/inventory', { params })
  return data
}

export type ReportExportFormat = 'Pdf' | 'Word'

export async function exportBusinessReport(
  params: ReportParams & { format: ReportExportFormat },
): Promise<{ blob: Blob; filename: string }> {
  const response = await apiClient.get('/reports/export', { params, responseType: 'blob' })
  const disposition = (response.headers['content-disposition'] as string | undefined) ?? ''
  const match = /filename="?([^";]+)"?/.exec(disposition)
  const format = params.format === 'Pdf' ? 'pdf' : 'docx'
  const filename = match?.[1] ?? `business-report-${params.from}-to-${params.to}.${format}`
  return { blob: response.data as Blob, filename }
}
