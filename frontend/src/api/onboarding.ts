import { apiClient } from '@/lib/api-client'
import type { Business, CompleteOnboardingRequest } from '@/types/business'
import type { AuthResult } from '@/types/auth'

export async function completeOnboarding(payload: CompleteOnboardingRequest): Promise<Business & AuthResult> {
  const { data } = await apiClient.post<Business & AuthResult>('/onboarding/complete', payload)
  return data
}
