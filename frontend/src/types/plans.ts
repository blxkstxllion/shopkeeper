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
  billingEnabled: boolean
  subscriptionStatus: string | null
  currentPeriodEnd: string | null
}

export interface CheckoutSession {
  authorizationUrl: string
}

export interface VerifyCheckoutResult {
  success: boolean
  newTier: PlanTier | null
}
