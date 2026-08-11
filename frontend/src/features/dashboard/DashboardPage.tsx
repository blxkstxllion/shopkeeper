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
} from 'lucide-react'
import {
  ResponsiveContainer,
  LineChart,
  Line,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
} from 'recharts'
import { useAuth } from '@/contexts/AuthContext'
import { useActiveBranch } from '@/hooks/useActiveBranch'
import { getDashboardSummary } from '@/api/dashboard'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/ui/EmptyState'
import { StatTile } from '@/components/ui/StatTile'
import { formatMoney, formatDateTime } from '@/lib/format'
import type { DashboardMetric, CategorySales, TopProduct, RecentTransaction } from '@/types/dashboard'

// Fixed categorical hue order from the design system's validated 8-slot palette -
// slots are assigned by position, never generated or cycled past 8.
const CATEGORY_SLOT_VARS = [
  '--cat-0',
  '--cat-1',
  '--cat-2',
  '--cat-3',
  '--cat-4',
  '--cat-5',
  '--cat-6',
  '--cat-7',
]

const STATUS_STYLES: Record<string, { label: string; className: string }> = {
  Completed: {
    label: 'Completed',
    className: 'bg-[#0ca30c]/10 text-[#0ca30c] dark:bg-[#0ca30c]/15',
  },
  Voided: {
    label: 'Voided',
    className: 'bg-[#d03b3b]/10 text-[#d03b3b] dark:bg-[#d03b3b]/15',
  },
  PartiallyRefunded: {
    label: 'Partially refunded',
    className: 'bg-[#fab219]/15 text-[#8a5a00] dark:bg-[#fab219]/15 dark:text-[#fab219]',
  },
  Refunded: {
    label: 'Refunded',
    className: 'bg-[#ec835a]/15 text-[#b0431b] dark:bg-[#ec835a]/20 dark:text-[#ec835a]',
  },
}

export function DashboardPage() {
  const { user, activeBusiness } = useAuth()
  const { branch } = useActiveBranch()

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard-summary', branch?.id],
    queryFn: () => getDashboardSummary(branch?.id),
  })

  return (
    <div
      className="mx-auto max-w-6xl [--cat-0:#2a78d6] [--cat-1:#1baf7a] [--cat-2:#eda100] [--cat-3:#008300] [--cat-4:#4a3aa7] [--cat-5:#e34948] [--cat-6:#e87ba4] [--cat-7:#eb6834] dark:[--cat-0:#3987e5] dark:[--cat-1:#199e70] dark:[--cat-2:#c98500] dark:[--cat-4:#9085e9] dark:[--cat-5:#e66767] dark:[--cat-6:#d55181] dark:[--cat-7:#d95926] [--chart-revenue:#2a78d6] dark:[--chart-revenue:#3987e5] [--chart-profit:#1baf7a] dark:[--chart-profit:#199e70] [--chart-grid:#e1e0d9] dark:[--chart-grid:#2c2c2a] [--chart-axis:#898781]"
    >
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">
          Welcome back, {user?.firstName}
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Here&apos;s what&apos;s happening at {activeBusiness?.businessName}
          {branch ? ` · ${branch.name}` : ''}.
        </p>
      </div>

      {isLoading || !data ? (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Card key={i} className="h-24 animate-pulse p-4" />
          ))}
        </div>
      ) : (
        <>
          <div className="mb-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <MetricCard label="Today's revenue" icon={Wallet} metric={data.todayRevenue} comparedTo="yesterday" />
            <MetricCard label="Today's profit" icon={TrendingUp} metric={data.todayProfit} comparedTo="yesterday" />
            <MetricCard label="Month revenue" icon={Wallet} metric={data.monthRevenue} comparedTo="last month" />
            <MetricCard label="Month profit" icon={TrendingUp} metric={data.monthProfit} comparedTo="last month" />
          </div>

          <div className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-3">
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

          <div className="mb-6 grid grid-cols-1 gap-4 lg:grid-cols-5">
            <Card className="p-4 lg:col-span-3">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
                Revenue &amp; profit, last 7 days
              </h2>
              <RevenueProfitTrendChart trend={data.revenueProfitTrend} />
            </Card>

            <Card className="p-4 lg:col-span-2">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
                Sales by category, today
              </h2>
              <CategoryBreakdown categories={data.salesByCategory} />
            </Card>
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <Card className="p-4">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
                Top products, this month
              </h2>
              <TopProductsList products={data.topProducts} />
            </Card>

            <Card className="p-4">
              <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Recent transactions</h2>
              <RecentTransactionsList transactions={data.recentTransactions} />
            </Card>
          </div>
        </>
      )}
    </div>
  )
}

function MetricCard({
  label,
  icon: Icon,
  metric,
  comparedTo,
}: {
  label: string
  icon: typeof Wallet
  metric: DashboardMetric
  comparedTo: string
}) {
  const isUp = metric.changePercent !== null && metric.changePercent >= 0
  return (
    <Card className="p-4">
      <div className="mb-2 flex items-center gap-2 text-slate-400">
        <Icon className="h-4 w-4" />
        <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
      </div>
      <p className="text-lg font-semibold text-slate-900 dark:text-slate-100">{formatMoney(metric.value)}</p>
      {metric.changePercent !== null && (
        <p
          className={`mt-1 inline-flex items-center gap-1 text-xs font-medium ${
            isUp ? 'text-[#006300] dark:text-[#0ca30c]' : 'text-[#d03b3b]'
          }`}
        >
          {isUp ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />}
          {Math.abs(metric.changePercent)}% vs {comparedTo}
        </p>
      )}
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
        <LineChart data={chartData} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
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
          <Line
            type="monotone"
            dataKey="revenue"
            name="Revenue"
            stroke="var(--chart-revenue)"
            strokeWidth={2}
            dot={{ r: 4, fill: 'var(--chart-revenue)', strokeWidth: 2, stroke: 'var(--color-surface)' }}
          />
          <Line
            type="monotone"
            dataKey="profit"
            name="Profit"
            stroke="var(--chart-profit)"
            strokeWidth={2}
            dot={{ r: 4, fill: 'var(--chart-profit)', strokeWidth: 2, stroke: 'var(--color-surface)' }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

function CategoryBreakdown({ categories }: { categories: CategorySales[] }) {
  if (categories.length === 0) {
    return (
      <EmptyState
        icon={LayoutGrid}
        title="No sales yet today"
        description="Category breakdown appears once today's first sale is rung up."
      />
    )
  }

  const shown = categories.slice(0, 8)

  return (
    <div>
      <div className="flex h-6 w-full overflow-hidden rounded" role="img" aria-label="Sales share by category">
        {shown.map((c, i) => (
          <div
            key={c.categoryName}
            className="h-full first:rounded-l last:rounded-r"
            style={{
              width: `${c.percentOfTotal}%`,
              backgroundColor: `var(${CATEGORY_SLOT_VARS[i % CATEGORY_SLOT_VARS.length]})`,
              marginRight: i < shown.length - 1 ? 2 : 0,
            }}
            title={`${c.categoryName}: ${c.percentOfTotal}%`}
          />
        ))}
      </div>
      <ul className="mt-4 space-y-2.5">
        {shown.map((c, i) => (
          <li key={c.categoryName} className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-2 text-slate-600 dark:text-slate-300">
              <span
                className="h-2.5 w-2.5 shrink-0 rounded-full"
                style={{ backgroundColor: `var(${CATEGORY_SLOT_VARS[i % CATEGORY_SLOT_VARS.length]})` }}
              />
              {c.categoryName}
            </span>
            <span className="text-slate-900 dark:text-slate-100">
              {formatMoney(c.revenue)} <span className="text-slate-400">· {c.percentOfTotal}%</span>
            </span>
          </li>
        ))}
      </ul>
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

  const maxRevenue = Math.max(...products.map((p) => p.revenue));

  return (
    <ul className="space-y-3">
      {products.map((p) => (
        <li key={p.productName}>
          <div className="mb-1 flex items-center justify-between text-sm">
            <span className="font-medium text-slate-900 dark:text-slate-100">{p.productName}</span>
            <span className="text-slate-500 dark:text-slate-400">
              {formatMoney(p.revenue)} <span className="text-slate-400">· {p.unitsSold} sold</span>
            </span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
            <div
              className="h-full rounded-full bg-[#2a78d6] dark:bg-[#3987e5]"
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
      <EmptyState icon={Receipt} title="No transactions yet" description="Sales made at the register will show up here." />
    )
  }

  return (
    <ul className="divide-y divide-slate-100 dark:divide-slate-800">
      {transactions.map((t) => {
        const status = STATUS_STYLES[t.status] ?? { label: t.status, className: 'bg-slate-100 text-slate-600' }
        return (
          <li key={t.saleId} className="flex items-center justify-between py-2.5 text-sm">
            <div>
              <p className="font-medium text-slate-900 dark:text-slate-100">{t.saleNumber}</p>
              <p className="text-xs text-slate-400">
                {t.cashierName} · {formatDateTime(t.createdAt)}
              </p>
            </div>
            <div className="text-right">
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
