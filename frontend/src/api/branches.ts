import { apiClient } from '@/lib/api-client'
import type { Branch } from '@/types/business'

export async function getBranches(): Promise<Branch[]> {
  const { data } = await apiClient.get<Branch[]>('/branches')
  return data
}
