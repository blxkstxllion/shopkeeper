import { apiClient } from '@/lib/api-client'
import type { JoinBusinessInfo, SubmitJoinRequestPayload } from '@/types/join'

export async function getBusinessByCode(code: string): Promise<JoinBusinessInfo> {
  const { data } = await apiClient.get<{ businessId: string; businessName: string }>(`/join/${code}`)
  return { businessId: data.businessId, businessName: data.businessName }
}

export async function submitJoinRequest(code: string, payload: SubmitJoinRequestPayload): Promise<void> {
  await apiClient.post(`/join/${code}/request`, payload)
}

export async function submitJoinRequestForExistingUser(code: string): Promise<void> {
  await apiClient.post(`/join/${code}/request-existing`)
}
