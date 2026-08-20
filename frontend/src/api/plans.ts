import { apiClient } from '@/lib/api-client'
import type { CheckoutSession, PlanTier, PlanUsage, VerifyCheckoutResult } from '@/types/plans'

export async function getPlanUsage(): Promise<PlanUsage> {
  const { data } = await apiClient.get<PlanUsage>('/plans/usage')
  return data
}

export async function setPlanTier(newTier: PlanTier): Promise<void> {
  await apiClient.post('/plans/tier', { newTier })
}

export async function setInventoryAddOn(enabled: boolean): Promise<void> {
  await apiClient.post('/plans/inventory-add-on', { enabled })
}

export async function initiateCheckout(requestedTier: PlanTier): Promise<CheckoutSession> {
  const { data } = await apiClient.post<CheckoutSession>('/plans/checkout', { requestedTier })
  return data
}

export async function verifyCheckout(reference: string): Promise<VerifyCheckoutResult> {
  const { data } = await apiClient.post<VerifyCheckoutResult>('/plans/checkout/verify', { reference })
  return data
}
