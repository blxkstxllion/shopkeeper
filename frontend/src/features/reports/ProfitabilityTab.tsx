import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Wallet, TrendingUp, TrendingDown, Receipt, Percent, Award, LayoutGrid, Download, Plus, X } from 'lucide-react'
import { ResponsiveContainer, LineChart, Line, CartesianGrid, XAxis, YAxis, Tooltip, Legend } from 'recharts'
import { getProfitabilityReport } from '@/api/reports'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatTile, type StatDelta } from '@/components/ui/StatTile'
import { Skeleton, StatTileSkeleton, ChartSkeleton, ListSkeleton } from '@/components/ui/Skeleton'
import { CategoryBreakdown } from '@/components/ui/CategoryBreakdown'
import { EmptyState } from '@/components/ui/EmptyState'
import { UpgradePrompt } from '@/components/ui/UpgradePrompt'
import { ApiError } from '@/lib/api-client'
import { formatMoney } from '@/lib/format'
import { downloadCsv } from '@/lib/csv'
import { computePercentChange, getComparisonPresets } from '@/lib/reportComparison'
import { DateRangePicker, type DateRange } from '@/components/ui/DateRangePicker'
import type { ProductProfit } from '@/types/reports'

export function ProfitabilityTab({ range, branchId }: { range: DateRange; branchId?: string }) {
  const [compareRange, setCompareRange] = useState<DateRange | null>(null)
  const [compareLabel, setCompareLabel] = useState('comparison range')
  const [showCompareOptions, setShowCompareOptions] = useState(false)
  const [showCustomComparePicker, setShowCustomComparePicker] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ['reports-profitability', range, branchId],
    queryFn: () => getProfitabilityReport({ from: range.from, to: range.to, branchId }),
  })

  const { data: compareData } = useQuery({
    queryKey: ['reports-profitability', compareRange, branchId],
    queryFn: () => getProfitabilityReport({ from: compareRange!.from, to: compareRange!.to, branchId }),
    enabled: compareRange !== null,
  })

  if (error instanceof ApiError && error.status === 403) {
    return (
      <UpgradePrompt
        title="Reports aren't available on your current plan"
        description="Upgrade your plan to see profitability, expenses, and inventory reports."
      />
    )
  }

  if (isLoading || !data) {
    return (
      <div className="flex flex-col gap-6">
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <StatTileSkeleton key={i} />
          ))}
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
          <Card className="p-4 lg:col-span-3">
            <Skeleton className="mb-4 h-4 w-44" />
            <ChartSkeleton height={280} />
          </Card>
          <Card className="p-4 lg:col-span-2">
            <Skeleton className="mb-4 h-4 w-40" />
            <ChartSkeleton height={280} />
          </Card>
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Card className="p-4">
            <Skeleton className="mb-4 h-4 w-32" />
            <ListSkeleton rows={4} />
          </Card>
          <Card className="p-4">
            <Skeleton className="mb-4 h-4 w-48" />
            <ListSkeleton rows={4} />
          </Card>
        </div>
      </div>
    )
  }

  const { totals } = data
  const isNetPositive = totals.netProfit >= 0
  const compareTotals = compareData?.totals
  const vsLabel = `vs ${compareLabel.toLowerCase()}`

  function delta(current: number, previous: number | undefined, goodDirection?: 'up' | 'down'): StatDelta | undefined {
    if (previous === undefined) return undefined
    return { percent: computePercentChange(current, previous), label: vsLabel, goodDirection }
  }

  function selectCompareRange(selected: DateRange, label: string) {
    setCompareRange(selected)
    setCompareLabel(label)
    setShowCompareOptions(false)
    setShowCustomComparePicker(false)
  }

  function clearCompare() {
    setCompareRange(null)
    setShowCompareOptions(false)
    setShowCustomComparePicker(false)
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {compareRange ? (
            <div className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
              <span>
                Comparing to <span className="font-medium text-slate-700 dark:text-slate-300">{compareLabel}</span> (
                {compareRange.from} – {compareRange.to})
              </span>
              <button
                type="button"
                onClick={clearCompare}
                className="flex items-center gap-0.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="h-3.5 w-3.5" />
                Clear
              </button>
            </div>
          ) : showCompareOptions ? (
            <div className="flex flex-wrap items-center gap-2">
              {getComparisonPresets(range).map((p) => (
                <button
                  key={p.label}
                  type="button"
                  onClick={() => selectCompareRange(p.range, p.label)}
                  className="rounded-full border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:border-primary-400 hover:text-primary-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-primary-500 dark:hover:text-primary-400"
                >
                  {p.label}
                </button>
              ))}
              <button
                type="button"
                onClick={() => setShowCustomComparePicker((s) => !s)}
                className="rounded-full border border-slate-200 px-3 py-1 text-xs font-medium text-slate-600 hover:border-primary-400 hover:text-primary-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-primary-500 dark:hover:text-primary-400"
              >
                Custom range
              </button>
              {showCustomComparePicker && (
                <DateRangePicker
                  value={compareRange ?? range}
                  onChange={(r) => selectCompareRange(r, 'Custom range')}
                />
              )}
              <button
                type="button"
                onClick={() => setShowCompareOptions(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                Cancel
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setShowCompareOptions(true)}
              className="flex items-center gap-1 rounded-full border border-dashed border-slate-300 px-3 py-1 text-xs font-medium text-slate-500 hover:border-primary-400 hover:text-primary-700 dark:border-slate-600 dark:text-slate-400 dark:hover:border-primary-500 dark:hover:text-primary-400"
            >
              <Plus className="h-3.5 w-3.5" />
              Compare
            </button>
          )}
        </div>

        <Button
          variant="secondary"
          size="sm"
          onClick={() =>
            downloadCsv(
              `profitability-${range.from}-to-${range.to}.csv`,
              ['Date', 'Revenue', 'Net profit'],
              data.dailyTrend.map((d) => [d.date, d.revenue, d.netProfit]),
            )
          }
        >
          <Download className="h-3.5 w-3.5" />
          Export CSV
        </Button>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3">
        <StatTile
          label="Revenue"
          icon={Wallet}
          value={formatMoney(totals.revenue)}
          delta={delta(totals.revenue, compareTotals?.revenue)}
        />
        <StatTile
          label="Cost of goods sold"
          icon={Receipt}
          value={formatMoney(totals.cogs)}
          delta={delta(totals.cogs, compareTotals?.cogs, 'down')}
        />
        <StatTile
          label="Gross profit"
          icon={TrendingUp}
          value={`${formatMoney(totals.grossProfit)} (${totals.grossMarginPercent}%)`}
          delta={delta(totals.grossProfit, compareTotals?.grossProfit)}
        />
        <StatTile
          label="Total expenses"
          icon={Receipt}
          value={formatMoney(totals.totalExpenses)}
          delta={delta(totals.totalExpenses, compareTotals?.totalExpenses, 'down')}
        />
        <StatTile
          label="Net profit"
          icon={isNetPositive ? TrendingUp : TrendingDown}
          value={`${formatMoney(totals.netProfit)} (${totals.netMarginPercent}%)`}
          tone={isNetPositive ? undefined : 'critical'}
          delta={delta(totals.netProfit, compareTotals?.netProfit)}
        />
        <StatTile
          label="Net margin"
          icon={Percent}
          value={`${totals.netMarginPercent}%`}
          delta={delta(totals.netMarginPercent, compareTotals?.netMarginPercent)}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
        <Card className="p-4 lg:col-span-3">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Revenue &amp; net profit</h2>
          <ProfitTrendChart trend={data.dailyTrend} />
        </Card>
        <Card className="p-4 lg:col-span-2">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Profit by category</h2>
          <CategoryBreakdown
            items={data.byCategory.map((c) => ({
              name: c.categoryName,
              value: c.profit,
              percentOfTotal: totals.grossProfit > 0 ? Math.round((c.profit / totals.grossProfit) * 1000) / 10 : 0,
            }))}
            emptyIcon={LayoutGrid}
            emptyTitle="No sales in this range"
            emptyDescription="Category profit breakdown appears once there's a sale in the selected date range."
          />
        </Card>
      </div>

      {data.byBranch.length > 0 && (
        <Card className="p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Branch comparison</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400 dark:border-slate-800">
                <th className="py-2 font-medium">Branch</th>
                <th className="py-2 text-right font-medium">Revenue</th>
                <th className="py-2 text-right font-medium">Profit</th>
                <th className="py-2 text-right font-medium">Margin</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {data.byBranch.map((b) => (
                <tr key={b.branchId}>
                  <td className="py-2.5 font-medium text-slate-900 dark:text-slate-100">{b.branchName}</td>
                  <td className="py-2.5 text-right text-slate-700 dark:text-slate-300">{formatMoney(b.revenue)}</td>
                  <td className="py-2.5 text-right text-slate-700 dark:text-slate-300">{formatMoney(b.profit)}</td>
                  <td className="py-2.5 text-right text-slate-500 dark:text-slate-400">{b.marginPercent}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card className="p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Sales by cashier</h2>
          {data.byCashier.length === 0 ? (
            <EmptyState
              icon={Award}
              title="No sales in this range"
              description="Cashier performance appears once there's a sale in the selected date range."
            />
          ) : (
            <ul className="divide-y divide-slate-100 dark:divide-slate-800">
              {data.byCashier.map((c) => (
                <li key={c.cashierUserId} className="flex items-center justify-between py-2.5 text-sm">
                  <div>
                    <p className="font-medium text-slate-900 dark:text-slate-100">{c.cashierName}</p>
                    <p className="text-xs text-slate-400">{c.salesCount} sales</p>
                  </div>
                  <div className="text-right">
                    <p className="text-slate-900 dark:text-slate-100">{formatMoney(c.revenue)}</p>
                    <p className="text-xs text-slate-400">{formatMoney(c.profit)} profit</p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className="p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">
            Top &amp; worst products by profit
          </h2>
          {data.topProducts.length === 0 ? (
            <EmptyState
              icon={Award}
              title="No sales in this range"
              description="Product profitability appears once there's a sale in the selected date range."
            />
          ) : (
            <div className="flex flex-col gap-4">
              <ProductRankList title="Top" products={data.topProducts} tone="good" />
              <ProductRankList title="Worst" products={data.worstProducts} tone="critical" />
            </div>
          )}
        </Card>
      </div>
    </div>
  )
}

function ProductRankList({
  title,
  products,
  tone,
}: {
  title: string
  products: ProductProfit[]
  tone: 'good' | 'critical'
}) {
  const toneClass = tone === 'good' ? 'text-good dark:text-good-dark' : 'text-danger dark:text-danger-dark'
  return (
    <div>
      <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-slate-400">{title}</p>
      <ul className="space-y-1.5">
        {products.map((p) => (
          <li key={p.productName} className="flex items-center justify-between text-sm">
            <span className="text-slate-700 dark:text-slate-300">{p.productName}</span>
            <span className={`font-medium ${toneClass}`}>{formatMoney(p.profit)}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

function ProfitTrendChart({ trend }: { trend: { date: string; revenue: number; netProfit: number }[] }) {
  const chartData = trend.map((p) => ({
    ...p,
    label: new Date(p.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
  }))

  return (
    <div style={{ width: '100%', height: 280 }}>
      <ResponsiveContainer>
        <LineChart data={chartData} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid stroke="var(--chart-grid)" vertical={false} />
          <XAxis
            dataKey="label"
            stroke="var(--chart-axis)"
            tick={{ fill: 'var(--chart-axis)', fontSize: 11 }}
            axisLine={{ stroke: 'var(--chart-axis)' }}
            tickLine={false}
            interval="preserveStartEnd"
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
            dot={false}
          />
          <Line
            type="monotone"
            dataKey="netProfit"
            name="Net profit"
            stroke="var(--chart-profit)"
            strokeWidth={2}
            dot={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
