import { apiClient } from '@/lib/api-client'
import type { BusinessAbout, UpdateBusinessAboutPayload } from '@/types/about'

export async function getBusinessAbout(): Promise<BusinessAbout> {
  const { data } = await apiClient.get<BusinessAbout>('/about')
  return data
}

export async function updateBusinessAbout(payload: UpdateBusinessAboutPayload): Promise<void> {
  await apiClient.put('/about', payload)
}
