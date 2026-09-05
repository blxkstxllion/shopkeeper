import { apiClient } from '@/lib/api-client'
import type { Branch, CreateBranchPayload, UpdateBranchPayload } from '@/types/business'

export async function getBranches(): Promise<Branch[]> {
  const { data } = await apiClient.get<Branch[]>('/branches')
  return data
}

export async function createBranch(payload: CreateBranchPayload & { clientRequestId?: string }): Promise<Branch> {
  const { data } = await apiClient.post<Branch>('/branches', payload)
  return data
}

export async function updateBranch(payload: UpdateBranchPayload & { clientRequestId?: string }): Promise<void> {
  await apiClient.put(`/branches/${payload.id}`, payload)
}

export async function deleteBranch(id: string, clientRequestId?: string): Promise<void> {
  await apiClient.delete(`/branches/${id}`, { params: { clientRequestId } })
}
