export interface AuditLog {
  id: string
  action: string
  entityType: string | null
  entityId: string | null
  actorName: string | null
  previousValue: string | null
  newValue: string | null
  ipAddress: string | null
  createdAt: string
}
