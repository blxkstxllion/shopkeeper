export type BusinessType =
  | 'Retail'
  | 'Restaurant'
  | 'Grocery'
  | 'Pharmacy'
  | 'Electronics'
  | 'Fashion'
  | 'Wholesale'
  | 'Services'
  | 'Other'

export type BusinessGoal =
  | 'IncreaseProfit'
  | 'ReduceStockLosses'
  | 'ImproveInventory'
  | 'ManageBranches'
  | 'TrackExpenses'
  | 'ImproveEmployeePerformance'

export interface Business {
  id: string
  name: string
  businessType: string
  country: string
  currencyCode: string
  logoUrl: string | null
  onboardingCompleted: boolean
  firstBranchId: string
}

export interface Branch {
  id: string
  name: string
  code: string
  city: string | null
  isMainBranch: boolean
  isActive: boolean
}

export interface CompleteOnboardingRequest {
  businessName: string
  businessType: BusinessType
  country: string
  currencyCode: string
  logoUrl?: string | null
  taxEnabled: boolean
  taxRatePercent: number
  taxInclusivePricing: boolean
  goals: BusinessGoal[]
  firstBranchName: string
  firstBranchAddress?: string | null
  firstBranchCity?: string | null
}
