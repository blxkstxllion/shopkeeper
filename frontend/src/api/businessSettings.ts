import { apiClient } from '@/lib/api-client'
import type { BusinessSettings, UpdateBusinessProfilePayload, UpdateTaxSettingsPayload } from '@/types/businessSettings'

export async function getBusinessSettings(): Promise<BusinessSettings> {
  const { data } = await apiClient.get<BusinessSettings>('/business-settings')
  return data
}

export async function updateBusinessProfile(payload: UpdateBusinessProfilePayload): Promise<void> {
  await apiClient.put('/business-settings/profile', payload)
}

export async function updateTaxSettings(payload: UpdateTaxSettingsPayload): Promise<void> {
  await apiClient.put('/business-settings/tax', payload)
}
