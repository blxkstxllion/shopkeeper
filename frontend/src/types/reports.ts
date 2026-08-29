export interface ProfitabilityTotals {
  revenue: number
  cogs: number
  grossProfit: number
  grossMarginPercent: number
  totalExpenses: number
  netProfit: number
  netMarginPercent: number
}

export interface DailyProfitPoint {
  date: string
  revenue: number
  netProfit: number
}

export interface CategoryProfit {
  categoryName: string
  revenue: number
  profit: number
}

export interface BranchProfit {
  branchId: string
  branchName: string
  revenue: number
  profit: number
  marginPercent: number
}

export interface CashierProfit {
  cashierUserId: string
  cashierName: string
  revenue: number
  profit: number
  salesCount: number
}

export interface ProductProfit {
  productName: string
  revenue: number
  profit: number
  unitsSold: number
}

export interface ProfitabilityReport {
  totals: ProfitabilityTotals
  dailyTrend: DailyProfitPoint[]
  byCategory: CategoryProfit[]
  byBranch: BranchProfit[]
  byCashier: CashierProfit[]
  topProducts: ProductProfit[]
  worstProducts: ProductProfit[]
}

export interface ExpenseCategoryTotal {
  categoryName: string
  amount: number
  percentOfTotal: number
}

export interface DailyExpensePoint {
  date: string
  amount: number
}

export interface ExpenseReport {
  totalAmount: number
  byCategory: ExpenseCategoryTotal[]
  dailyTrend: DailyExpensePoint[]
}

export interface InventoryValuation {
  totalProducts: number
  lowStockCount: number
  outOfStockCount: number
  inventoryValue: number
}

export interface StockAlertProduct {
  productId: string
  productName: string
  quantityOnHand: number
  minimumStock: number
}

export interface ProductTurnover {
  productId: string
  productName: string
  unitsSoldInRange: number
  quantityOnHand: number
  turnoverRatio: number | null
}

export interface InventoryReport {
  valuation: InventoryValuation
  lowStockProducts: StockAlertProduct[]
  outOfStockProducts: StockAlertProduct[]
  turnover: ProductTurnover[]
}
