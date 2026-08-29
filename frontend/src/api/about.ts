import { apiClient } from '@/lib/api-client'
import type { BusinessAbout, UpdateBusinessAboutPayload } from '@/types/about'

export async function getBusinessAbout(): Promise<BusinessAbout> {
  const { data } = await apiClient.get<BusinessAbout>('/about')
  return data
}

export async function updateBusinessAbout(payload: UpdateBusinessAboutPayload): Promise<void> {
  await apiClient.put('/about', payload)
}

export async function uploadBusinessLogo(file: File): Promise<{ url: string }> {
  const formData = new FormData()
  formData.append('file', file)
  const { data } = await apiClient.post<{ url: string }>('/uploads/business-logo', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}
