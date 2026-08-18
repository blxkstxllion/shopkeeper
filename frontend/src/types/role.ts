export interface RoleManagement {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  permissionKeys: string[]
  employeeCount: number
}

export interface PermissionCatalogItem {
  key: string
  name: string
  category: string
}

export interface RolePayload {
  name: string
  description: string | null
  permissionKeys: string[]
}
