import { apiClient } from '@/lib/api-client'
import type { PlanTier, PlanUsage } from '@/types/plans'

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
