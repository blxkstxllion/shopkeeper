import { apiClient } from '@/lib/api-client'
import type { DashboardSummary } from '@/types/dashboard'

export async function getDashboardSummary(branchId?: string): Promise<DashboardSummary> {
  const { data } = await apiClient.get<DashboardSummary>('/dashboard', { params: { branchId } })
  return data
}
