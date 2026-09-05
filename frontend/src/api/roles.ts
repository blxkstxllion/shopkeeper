import { apiClient } from '@/lib/api-client'
import type { Role } from '@/types/employee'
import type { PermissionCatalogItem, RoleManagement, RolePayload } from '@/types/role'

export async function getRoles(): Promise<Role[]> {
  const { data } = await apiClient.get<Role[]>('/roles')
  return data
}

export async function getRoleManagement(): Promise<RoleManagement[]> {
  const { data } = await apiClient.get<RoleManagement[]>('/roles/management')
  return data
}

export async function getPermissionCatalog(): Promise<PermissionCatalogItem[]> {
  const { data } = await apiClient.get<PermissionCatalogItem[]>('/roles/permissions')
  return data
}

export async function createRole(payload: RolePayload & { clientRequestId?: string }): Promise<string> {
  const { data } = await apiClient.post<string>('/roles', payload)
  return data
}

export async function updateRole(payload: RolePayload & { id: string; clientRequestId?: string }): Promise<void> {
  await apiClient.put(`/roles/${payload.id}`, payload)
}

export async function deleteRole(id: string, clientRequestId?: string): Promise<void> {
  await apiClient.delete(`/roles/${id}`, { params: { clientRequestId } })
}
