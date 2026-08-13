import { apiClient } from '@/lib/api-client'
import type { AuthResult } from '@/types/auth'
import type {
  AcceptInvitationPayload,
  BusinessUsersResponse,
  InviteEmployeePayload,
  InvitationDetails,
} from '@/types/employee'

export async function getBusinessUsers(): Promise<BusinessUsersResponse> {
  const { data } = await apiClient.get<BusinessUsersResponse>('/employees')
  return data
}

export async function inviteEmployee(payload: InviteEmployeePayload): Promise<void> {
  await apiClient.post('/employees/invite', payload)
}

export async function removeEmployee(businessUserId: string): Promise<void> {
  await apiClient.delete(`/employees/${businessUserId}`)
}

export async function getInvitation(token: string): Promise<InvitationDetails> {
  const { data } = await apiClient.get<InvitationDetails>(`/employees/invitations/${token}`)
  return data
}

export async function acceptInvitation(token: string, payload: AcceptInvitationPayload): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>(`/employees/invitations/${token}/accept`, payload)
  return data
}

export async function acceptInvitationForExistingUser(token: string): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>(`/employees/invitations/${token}/accept-existing`)
  return data
}
