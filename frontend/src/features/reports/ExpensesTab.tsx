import { useQuery } from '@tanstack/react-query'
import { Receipt, LayoutGrid, Download } from 'lucide-react'
import { ResponsiveContainer, LineChart, Line, CartesianGrid, XAxis, YAxis, Tooltip } from 'recharts'
import { getExpenseReport } from '@/api/reports'
import { Card } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatTile } from '@/components/ui/StatTile'
import { CategoryBreakdown } from '@/components/ui/CategoryBreakdown'
import { formatMoney } from '@/lib/format'
import { downloadCsv } from '@/lib/csv'
import type { DateRange } from '@/components/ui/DateRangePicker'

export function ExpensesTab({ range, branchId }: { range: DateRange; branchId?: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['reports-expenses', range, branchId],
    queryFn: () => getExpenseReport({ from: range.from, to: range.to, branchId }),
  })

  if (isLoading || !data) {
    return (
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {Array.from({ length: 2 }).map((_, i) => (
          <Card key={i} className="h-24 animate-pulse p-4" />
        ))}
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <StatTile label="Total expenses" icon={Receipt} value={formatMoney(data.totalAmount)} />
        <Button
          variant="secondary"
          size="sm"
          onClick={() =>
            downloadCsv(
              `expenses-${range.from}-to-${range.to}.csv`,
              ['Date', 'Amount'],
              data.dailyTrend.map((d) => [d.date, d.amount]),
            )
          }
        >
          <Download className="h-3.5 w-3.5" />
          Export CSV
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-5">
        <Card className="p-4 lg:col-span-3">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">Daily spend</h2>
          <ExpenseTrendChart trend={data.dailyTrend} />
        </Card>
        <Card className="p-4 lg:col-span-2">
          <h2 className="mb-4 text-sm font-semibold text-slate-900 dark:text-slate-100">By category</h2>
          <CategoryBreakdown
            items={data.byCategory.map((c) => ({
              name: c.categoryName,
              value: c.amount,
              percentOfTotal: c.percentOfTotal,
            }))}
            emptyIcon={LayoutGrid}
            emptyTitle="No expenses in this range"
            emptyDescription="Category breakdown appears once an expense is recorded in the selected date range."
          />
        </Card>
      </div>
    </div>
  )
}

function ExpenseTrendChart({ trend }: { trend: { date: string; amount: number }[] }) {
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
          <Line
            type="monotone"
            dataKey="amount"
            name="Expenses"
            stroke="var(--chart-revenue)"
            strokeWidth={2}
            dot={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
