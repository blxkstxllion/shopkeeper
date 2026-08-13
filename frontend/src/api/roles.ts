import { apiClient } from '@/lib/api-client'
import type { Role } from '@/types/employee'

export async function getRoles(): Promise<Role[]> {
  const { data } = await apiClient.get<Role[]>('/roles')
  return data
}
