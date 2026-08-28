export interface AppNotification {
  id: string
  type: string
  title: string
  message: string
  link: string | null
  isRead: boolean
  createdAt: string
}

export interface NotificationPreferences {
  notifyOnJoinRequest: boolean
  notifyOnLowStock: boolean
}
