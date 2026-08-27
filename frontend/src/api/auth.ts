import { apiClient } from '@/lib/api-client'
import type { AuthResult, LoginResponse, Session, TwoFactorSetup, TwoFactorStatus, User } from '@/types/auth'

export interface RegisterPayload {
  email: string
  password: string
  firstName: string
  lastName: string
}

export interface LoginPayload {
  email: string
  password: string
  businessId?: string | null
}

export async function register(payload: RegisterPayload): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/register', payload)
  return data
}

export async function login(payload: LoginPayload): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', payload)
  return data
}

export async function verifyTwoFactor(challengeToken: string, code: string): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/2fa/verify', { challengeToken, code })
  return data
}

export async function getTwoFactorStatus(): Promise<TwoFactorStatus> {
  const { data } = await apiClient.get<TwoFactorStatus>('/auth/2fa/status')
  return data
}

export async function setupTwoFactor(): Promise<TwoFactorSetup> {
  const { data } = await apiClient.post<TwoFactorSetup>('/auth/2fa/setup')
  return data
}

export async function enableTwoFactor(code: string): Promise<{ recoveryCodes: string[] }> {
  const { data } = await apiClient.post<{ recoveryCodes: string[] }>('/auth/2fa/enable', { code })
  return data
}

export async function disableTwoFactor(password: string): Promise<void> {
  await apiClient.post('/auth/2fa/disable', { password })
}

export async function getSessions(): Promise<Session[]> {
  const { data } = await apiClient.get<Session[]>('/auth/sessions')
  return data
}

export async function revokeSession(id: string): Promise<void> {
  await apiClient.delete(`/auth/sessions/${id}`)
}

export async function revokeOtherSessions(): Promise<void> {
  await apiClient.post('/auth/sessions/revoke-others')
}

export async function logout(): Promise<void> {
  await apiClient.post('/auth/logout')
}

export async function refresh(): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/refresh')
  return data
}

export async function forgotPassword(email: string): Promise<void> {
  await apiClient.post('/auth/forgot-password', { email })
}

export async function resetPassword(token: string, newPassword: string): Promise<void> {
  await apiClient.post('/auth/reset-password', { token, newPassword })
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await apiClient.post('/auth/change-password', { currentPassword, newPassword })
}

export async function verifyEmail(token: string): Promise<void> {
  await apiClient.post('/auth/verify-email', { token })
}

export async function switchBusiness(businessId: string): Promise<AuthResult> {
  const { data } = await apiClient.post<AuthResult>('/auth/switch-business', { businessId })
  return data
}

export async function getCurrentUser(): Promise<User> {
  const { data } = await apiClient.get<User>('/users/me')
  return data
}

export async function uploadProfilePhoto(file: File): Promise<{ url: string }> {
  const formData = new FormData()
  formData.append('file', file)
  const { data } = await apiClient.post<{ url: string }>('/uploads/profile-photo', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export async function updateProfilePhoto(photoUrl: string): Promise<void> {
  await apiClient.put('/users/me/photo', { photoUrl })
}
