export type PlanTier = 'Free' | 'Business' | 'BusinessAi' | 'Enterprise' | 'EnterpriseAi'

export interface PlanLimitInfo {
  maxBranches: number
  maxProducts: number
  maxStaff: number
  hasReports: boolean
  hasAi: boolean
  hasCustomRoles: boolean
}

export interface PlanUsage {
  currentTier: PlanTier
  limits: PlanLimitInfo
  branchCount: number
  productCount: number
  staffCount: number
  hasUnlimitedInventoryAddOn: boolean
}
