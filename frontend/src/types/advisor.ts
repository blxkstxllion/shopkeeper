export type AdvisorQuestionId =
  | 'RevenueThisMonth'
  | 'ProfitMargin'
  | 'LowStock'
  | 'BestSellingProduct'
  | 'WorstPerformingProduct'
  | 'BranchComparison'
  | 'TopExpenseCategories'
  | 'AmIProfitable'

export interface AdvisorQuestion {
  id: AdvisorQuestionId
  label: string
}

export interface AdvisorAnswer {
  answer: string
  generatedAt: string
}

export interface AdvisorCapabilities {
  freeTextEnabled: boolean
}
