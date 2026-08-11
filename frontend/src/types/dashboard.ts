export interface DashboardMetric {
  value: number
  previousValue: number
  changePercent: number | null
}

export interface DailyTrendPoint {
  date: string
  revenue: number
  profit: number
}

export interface CategorySales {
  categoryName: string
  revenue: number
  percentOfTotal: number
}

export interface TopProduct {
  productName: string
  revenue: number
  unitsSold: number
}

export interface RecentTransaction {
  saleId: string
  saleNumber: string
  cashierName: string
  total: number
  status: string
  createdAt: string
}

export interface DashboardSummary {
  todayRevenue: DashboardMetric
  todayProfit: DashboardMetric
  monthRevenue: DashboardMetric
  monthProfit: DashboardMetric
  inventoryValue: number
  lowStockCount: number
  outOfStockCount: number
  revenueProfitTrend: DailyTrendPoint[]
  salesByCategory: CategorySales[]
  topProducts: TopProduct[]
  recentTransactions: RecentTransaction[]
}
