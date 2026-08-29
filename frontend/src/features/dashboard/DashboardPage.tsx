import { useQuery } from '@tanstack/react-query'
import {
  Wallet,
  TrendingUp,
  TrendingDown,
  Package,
  AlertTriangle,
  PackageX,
  Award,
  Receipt,
  LayoutGrid,
  AlertCircle,
  RefreshCw,
  ShoppingCart,
  PackagePlus,
} from 'lucide-react'
import {
  ResponsiveContainer,
  ComposedChart,
  Area,
  LineChart,
  Line,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
} from 'recharts'
import { Link } from 'react-router-dom'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { useCountUp } from '@/hooks/useCountUp'
import { getDashboardSummary } from '@/api/dashboard'
import { DashboardHeader } from './DashboardHeader'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/ui/EmptyState'
import { StatTile } from '@/components/ui/StatTile'
import { Skeleton, StatTileSkeleton, ChartSkeleton, ListSkeleton } from '@/components/ui/Skeleton'
import { CategoryBreakdown } from '@/components/ui/CategoryBreakdown'
import { formatMoney, formatDateTime } from '@/lib/format'
import type { DashboardMetric, TopProduct, RecentTransaction } from '@/types/dashboard'

const STATUS_STYLES: Record<string, { label: string; className: string }> = {
  Completed: {
    label: 'Completed',
    className: 'bg-good-dark/10 text-good-dark dark:bg-good-dark/15',
  },
  Voided: {
    label: 'Voided',
    className: 'bg-danger/10 text-danger dark:bg-danger/15 dark:text-danger-dark',
  },
  PartiallyRefunded: {
    label: 'Partially refunded',
    className: 'bg-warning-dark/15 text-warning dark:bg-warning-dark/15 dark:text-warning-dark',
  },
  Refunded: {
    label: 'Refunded',
    className: 'bg-serious-dark/15 text-serious dark:bg-serious-dark/20 dark:text-serious-dark',
  },
}

export function DashboardPage() {
  const { branch } = useActiveBranch()

  const { data, isLoading, isError, refetch, isRefetching, dataUpdatedAt } = useQuery({
    queryKey: ['dashboard-summary', branch?.id],
    queryFn: () => getDashboardSummary(branch?.id),
  })

  return (
    <div className="mx-auto max-w-6xl [--cat-0:#2a78d6] [--cat-1:#1baf7a] [--cat-2:#eda100] [--cat-3:#008300] [--cat-4:#4a3aa7] [--cat-5:#e34948] [--cat-6:#e87ba4] [--cat-7:#eb6834] dark:[--cat-0:#3987e5] dark:[--cat-1:#199e70] dark:[--cat-2:#c98500] dark:[--cat-4:#9085e9] dark:[--cat-5:#e66767] dark:[--cat-6:#d55181] dark:[--cat-7:#d95926] [--chart-revenue:#2a78d6] dark:[--chart-revenue:#3987e5] [--chart-profit:#1baf7a] dark:[--chart-profit:#199e70] [--chart-grid:#e1e0d9] dark:[--chart-grid:#2c2c2a] [--chart-axis:#898781]">
      <DashboardHeader />

      {isError && (
        <div className="mb-4 flex animate-[fade-up_0.4s_ease-out_both] flex-wrap items-center gap-3 rounded-xl border border-danger/20 bg-danger/5 px-4 py-3 text-sm text-danger dark:border-danger-dark/30 dark:bg-danger-dark/10 dark:text-danger-dark">
          <AlertCircle className="h-4 w-4 shrink-0" />
          <span className="flex-1">Unable to reach the server. Please check your connection and try again.</span>
          <button
            type="button"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="inline-flex items-center gap-1.5 rounded-lg bg-danger px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-danger/90 disabled:opacity-60 dark:bg-danger-dark dark:text-slate-900"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
            Retry
          </button>
        </div>
      )}

      {isLoading || !data ? (
        <>
          <div className="mb-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <StatTileSkeleton key={i} />
            ))}
          </div>
          <div className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <StatTileSkeleton key={i} />
            ))}
          </div>
          <div className="mb-6 grid grid-cols-1 gap-4 lg:grid-cols-5">
            <Card className="p-4 lg:col-span-3">
              <Skeleton className="mb-4 h-4 w-48" />
              <ChartSkeleton height={260} />
            </Card>
            <Card className="p-4 lg:col-span-2">
              <Skeleton className="mb-4 h-4 w-40" />
              <ChartSkeleton height={260} />
            </Card>
          </div>
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Card className="p-4">
              <Skeleton className="mb-4 h-4 w-32" />
              <ListSkeleton rows={4} />
            </Card>
            <Card className="p-4">
              <Skeleton className="mb-4 h-4 w-36" />
              <ListSkeleton rows={4} />
            </Card>
          </div>
        </>
      ) : (
        <>
          <div className="mb-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <MetricCard
              label="Today's revenue"
              icon={Wallet}
              metric={data.todayRevenue}
              comparedTo="yesterday"
              sparkline={data.revenueProfitTrend.map((p) => p.revenue)}
              delayMs={0}
            />
            <MetricCard
              label="Today's profit"
              icon={TrendingUp}
              metric={data.todayProfit}
              comparedTo="yesterday"
              sparkline={data.revenueProfitTrend.map((p) => p.profit)}
              delayMs={60}
            />
            <MetricCard
              label="Month revenue"
              icon={Wallet}
              metric={data.monthRevenue}
              comparedTo="last month"
              sparkline={data.revenueProfitTrend.map((p) => p.revenue)}
              delayMs={120}
            />
            <MetricCard
              label="Month profit"
              icon={TrendingUp}
              metric={data.monthProfit}
              comparedTo="last month"
              sparkline={data.revenueProfitTrend.map((p) => p.profit)}
              delayMs={180}
            />
          </div>

          <div
            className="mb-6 grid animate-[fade-up_0.5s_ease-out_both] grid-cols-1 gap-3 sm:grid-cols-3"
            style={{ animationDelay: '0.15s' }}
          >
            <StatTile label="Inventory value" icon={Package} value={formatMoney(data.inventoryValue)} />
            <StatTile
              label="Low stock items"
              icon={AlertTriangle}
              value={String(data.lowStockCount)}
              tone={data.lowStockCount > 0 ? 'warning' : undefined}
            />
            <StatTile
              label="Out of stock"
              icon={PackageX}
              value={String(data.outOfStockCount)}
              tone={data.outOfStockCount > 0 ? 'critical' : undefined}
            />
          </div>

          {data.lowStockCount === 0 && data.outOfStockCount === 0 && data.recentTransactions.length === 0 && (
            <EmptyDashboardHint />
          )}

          <div
            className="mb-6 grid animate-[fade-up_0.5s_ease-out_both] grid-cols-1 gap-4 lg:grid-cols-5"
            style={{ animationDelay: '0.2s' }}
          >
            <Card className="p-4 lg:col-span-3">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                  Revenue &amp; profit, last 7 days
                </h2>
                <span className="flex items-center gap-1.5 text-xs text-slate-400">
                  <span className="h-1.5 w-1.5 rounded-full bg-good dark:bg-good-dark" />
                  Updated {formatDateTime(new Date(dataUpdatedAt).toISOString())}
                </span>
              </div>
              <RevenueProfitTrendChart trend={data.revenueProfitTrend} />
            </Card>

            <Card className="p-4 lg:col-span-2">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
                Sales by category, today
              </h2>
              <CategoryBreakdown
                items={data.salesByCategory.map((c) => ({
                  name: c.categoryName,
                  value: c.revenue,
                  percentOfTotal: c.percentOfTotal,
                }))}
                emptyIcon={LayoutGrid}
                emptyTitle="No sales yet today"
                emptyDescription="Category breakdown appears once today's first sale is rung up."
              />
            </Card>
          </div>

          <div
            className="grid animate-[fade-up_0.5s_ease-out_both] grid-cols-1 gap-4 lg:grid-cols-2"
            style={{ animationDelay: '0.25s' }}
          >
            <Card className="p-4">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
                Top products, this month
              </h2>
              <TopProductsList products={data.topProducts} />
            </Card>

            <Card className="p-4">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Recent activity</h2>
              <RecentTransactionsList transactions={data.recentTransactions} />
            </Card>
          </div>
        </>
      )}
    </div>
  )
}

function EmptyDashboardHint() {
  return (
    <div className="mb-6 flex animate-[fade-up_0.5s_ease-out_both] items-center gap-3 rounded-xl border border-primary-200 bg-primary-50/60 px-4 py-3 text-sm text-primary-800 dark:border-primary-800 dark:bg-primary-900/10 dark:text-primary-200">
      <ShoppingCart className="h-4 w-4 shrink-0" />
      <span className="flex-1">Nothing's moved yet - ring up your first sale or add a product to get started.</span>
      <Link
        to="/app/inventory"
        className="inline-flex items-center gap-1.5 rounded-lg bg-primary-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-primary-700"
      >
        <PackagePlus className="h-3.5 w-3.5" />
        Add a product
      </Link>
    </div>
  )
}

function MetricCard({
  label,
  icon: Icon,
  metric,
  comparedTo,
  sparkline,
  delayMs,
}: {
  label: string
  icon: typeof Wallet
  metric: DashboardMetric
  comparedTo: string
  sparkline: number[]
  delayMs: number
}) {
  const isUp = metric.changePercent !== null && metric.changePercent >= 0
  const animatedValue = useCountUp(metric.value)
  const sparkPoints = sparkline.filter((n) => Number.isFinite(n))

  return (
    <Card
      className="animate-[fade-up_0.5s_ease-out_both] rounded-2xl p-4 transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md"
      style={{ animationDelay: `${delayMs}ms` }}
    >
      <div className="mb-2 flex items-center justify-between">
        <div className="flex items-center gap-2 text-slate-400">
          <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400">
            <Icon className="h-3.5 w-3.5" />
          </span>
          <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
        </div>
      </div>
      <div className="flex items-end justify-between gap-2">
        <div>
          <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">{formatMoney(animatedValue)}</p>
          {metric.changePercent !== null && (
            <p
              className={`mt-1 inline-flex items-center gap-1 text-xs font-medium ${
                isUp ? 'text-good dark:text-good-dark' : 'text-danger dark:text-danger-dark'
              }`}
            >
              {isUp ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />}
              {Math.abs(metric.changePercent)}% vs {comparedTo}
            </p>
          )}
        </div>
        {sparkPoints.length >= 2 && (
          <div className="h-8 w-16 shrink-0">
            <ResponsiveContainer>
              <LineChart data={sparkPoints.map((value, i) => ({ i, value }))}>
                <Line
                  type="monotone"
                  dataKey="value"
                  stroke={isUp ? 'var(--color-good)' : 'var(--color-danger)'}
                  strokeWidth={1.75}
                  dot={false}
                  isAnimationActive
                  animationDuration={700}
                  animationEasing="ease-out"
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </div>
    </Card>
  )
}

function RevenueProfitTrendChart({ trend }: { trend: { date: string; revenue: number; profit: number }[] }) {
  const chartData = trend.map((p) => ({
    ...p,
    label: new Date(p.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
  }))

  return (
    <div style={{ width: '100%', height: 260 }}>
      <ResponsiveContainer>
        <ComposedChart data={chartData} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
          <defs>
            <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor="var(--chart-revenue)" stopOpacity={0.18} />
              <stop offset="95%" stopColor="var(--chart-revenue)" stopOpacity={0} />
            </linearGradient>
            <linearGradient id="profitFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor="var(--chart-profit)" stopOpacity={0.18} />
              <stop offset="95%" stopColor="var(--chart-profit)" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid stroke="var(--chart-grid)" vertical={false} />
          <XAxis
            dataKey="label"
            stroke="var(--chart-axis)"
            tick={{ fill: 'var(--chart-axis)', fontSize: 12 }}
            axisLine={{ stroke: 'var(--chart-axis)' }}
            tickLine={false}
          />
          <YAxis
            stroke="var(--chart-axis)"
            tick={{ fill: 'var(--chart-axis)', fontSize: 12 }}
            axisLine={false}
            tickLine={false}
            width={48}
            tickFormatter={(v: number) => v.toLocaleString('en-US')}
          />
          <Tooltip
            cursor={{ stroke: 'var(--chart-axis)', strokeWidth: 1 }}
            contentStyle={{ borderRadius: 8, fontSize: 12, border: '1px solid var(--chart-grid)' }}
            formatter={(value) => formatMoney(Number(value))}
          />
          <Legend wrapperStyle={{ fontSize: 12 }} iconType="line" />
          <Area
            type="monotone"
            dataKey="revenue"
            name="Revenue"
            stroke="var(--chart-revenue)"
            strokeWidth={2}
            fill="url(#revenueFill)"
            dot={{ r: 4, fill: 'var(--chart-revenue)', strokeWidth: 2, stroke: 'var(--color-surface)' }}
            isAnimationActive
            animationDuration={900}
            animationEasing="ease-out"
          />
          <Area
            type="monotone"
            dataKey="profit"
            name="Profit"
            stroke="var(--chart-profit)"
            strokeWidth={2}
            fill="url(#profitFill)"
            dot={{ r: 4, fill: 'var(--chart-profit)', strokeWidth: 2, stroke: 'var(--color-surface)' }}
            isAnimationActive
            animationDuration={900}
            animationEasing="ease-out"
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  )
}

function TopProductsList({ products }: { products: TopProduct[] }) {
  if (products.length === 0) {
    return (
      <EmptyState
        icon={Award}
        title="No sales this month yet"
        description="Your best-selling products will show up here once sales come in."
      />
    )
  }

  const maxRevenue = Math.max(...products.map((p) => p.revenue))

  return (
    <ul className="space-y-3">
      {products.map((p, i) => (
        <li
          key={p.productName}
          className="animate-[fade-up_0.4s_ease-out_both]"
          style={{ animationDelay: `${i * 60}ms` }}
        >
          <div className="mb-1 flex items-center justify-between text-sm">
            <span className="flex items-center gap-2 font-medium text-slate-900 dark:text-slate-100">
              <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-primary-50 text-[11px] font-semibold text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
                {i + 1}
              </span>
              {p.productName}
            </span>
            <span className="text-slate-500 dark:text-slate-400">
              {formatMoney(p.revenue)} <span className="text-slate-400">· {p.unitsSold} sold</span>
            </span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
            <div
              className="h-full rounded-full bg-[#2a78d6] transition-all duration-700 ease-out dark:bg-[#3987e5]"
              style={{ width: `${maxRevenue > 0 ? (p.revenue / maxRevenue) * 100 : 0}%` }}
            />
          </div>
        </li>
      ))}
    </ul>
  )
}

function RecentTransactionsList({ transactions }: { transactions: RecentTransaction[] }) {
  if (transactions.length === 0) {
    return (
      <EmptyState
        icon={Receipt}
        title="No transactions yet"
        description="Sales made at the register will show up here."
      />
    )
  }

  return (
    <ul className="divide-y divide-slate-100 dark:divide-slate-800">
      {transactions.map((t, i) => {
        const status = STATUS_STYLES[t.status] ?? { label: t.status, className: 'bg-slate-100 text-slate-600' }
        return (
          <li
            key={t.saleId}
            className="flex animate-[fade-up_0.4s_ease-out_both] items-center gap-3 py-2.5 text-sm"
            style={{ animationDelay: `${i * 60}ms` }}
          >
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-600 dark:bg-primary-900/30 dark:text-primary-400">
              <Receipt className="h-3.5 w-3.5" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="font-medium text-slate-900 dark:text-slate-100">{t.saleNumber}</p>
              <p className="truncate text-xs text-slate-400">
                {t.cashierName} · {formatDateTime(t.createdAt)}
              </p>
            </div>
            <div className="shrink-0 text-right">
              <p className="font-medium text-slate-900 dark:text-slate-100">{formatMoney(t.total)}</p>
              <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${status.className}`}>
                {status.label}
              </span>
            </div>
          </li>
        )
      })}
    </ul>
  )
}
