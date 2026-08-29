export type BusinessType =
  'Retail' | 'Restaurant' | 'Grocery' | 'Pharmacy' | 'Electronics' | 'Fashion' | 'Wholesale' | 'Services' | 'Other'

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
  businessTypeOther: string | null
  country: string
  currencyCode: string
  logoUrl: string | null
  colorTheme: string
  onboardingCompleted: boolean
  firstBranchId: string
}

export interface Branch {
  id: string
  name: string
  code: string
  address: string | null
  city: string | null
  country: string | null
  phone: string | null
  email: string | null
  isMainBranch: boolean
  isActive: boolean
}

export interface CreateBranchPayload {
  name: string
  code: string
  address?: string | null
  city?: string | null
  country?: string | null
  phone?: string | null
  email?: string | null
}

export type UpdateBranchPayload = CreateBranchPayload & { id: string; isMain: boolean; isActive: boolean }

export interface CompleteOnboardingRequest {
  businessName: string
  businessType: BusinessType
  businessTypeOther?: string | null
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
  colorTheme?: string
}
