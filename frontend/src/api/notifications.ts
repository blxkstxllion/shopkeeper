import { apiClient } from '@/lib/api-client'
import type { PagedResult } from '@/types/product'
import type { AppNotification } from '@/types/notification'

export async function getNotifications(page = 1, pageSize = 30): Promise<PagedResult<AppNotification>> {
  const { data } = await apiClient.get<PagedResult<AppNotification>>('/notifications', { params: { page, pageSize } })
  return data
}

export async function getUnreadNotificationCount(): Promise<number> {
  const { data } = await apiClient.get<number>('/notifications/unread-count')
  return data
}

export async function markNotificationRead(id: string): Promise<void> {
  await apiClient.post(`/notifications/${id}/read`)
}

export async function markAllNotificationsRead(): Promise<void> {
  await apiClient.post('/notifications/read-all')
}
