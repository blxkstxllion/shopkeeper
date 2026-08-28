import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/types/product'
import type { AuditLog } from '@/types/auditLog'

export interface GetAuditLogsParams {
  entityType?: string
  action?: string
  userId?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export async function getAuditLogs(params: GetAuditLogsParams): Promise<PagedResult<AuditLog>> {
  const { data } = await apiClient.get<PagedResult<AuditLog>>('/audit-logs', { params })
  return data
}
