import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { TrendingUp, Receipt, Package } from 'lucide-react'
import { useBranchContext } from '@/contexts/BranchContext'
import { ProfitabilityTab } from './ProfitabilityTab'
import { ExpensesTab } from './ExpensesTab'
import { InventoryTab } from './InventoryTab'
import { DateRangePicker, type DateRange } from '@/components/ui/DateRangePicker'

type TabId = 'profitability' | 'expenses' | 'inventory'

const TABS: { id: TabId; label: string; icon: typeof TrendingUp }[] = [
  { id: 'profitability', label: 'Profitability', icon: TrendingUp },
  { id: 'expenses', label: 'Expenses', icon: Receipt },
  { id: 'inventory', label: 'Inventory', icon: Package },
]

function defaultRange(): DateRange {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 29)
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) }
}

// CSS custom properties consumed by the shared CategoryBreakdown/chart components -
// same palette slots as the Dashboard so the two surfaces read as one visual language.
const CHART_VARS =
  '[--cat-0:#2a78d6] [--cat-1:#1baf7a] [--cat-2:#eda100] [--cat-3:#008300] [--cat-4:#4a3aa7] [--cat-5:#e34948] [--cat-6:#e87ba4] [--cat-7:#eb6834] dark:[--cat-0:#3987e5] dark:[--cat-1:#199e70] dark:[--cat-2:#c98500] dark:[--cat-4:#9085e9] dark:[--cat-5:#e66767] dark:[--cat-6:#d55181] dark:[--cat-7:#d95926] [--chart-revenue:#2a78d6] dark:[--chart-revenue:#3987e5] [--chart-profit:#1baf7a] dark:[--chart-profit:#199e70] [--chart-grid:#e1e0d9] dark:[--chart-grid:#2c2c2a] [--chart-axis:#898781]'

export function ReportsPage() {
  const { branches, activeBranchId, canSwitchBranches } = useBranchContext()
  const [searchParams] = useSearchParams()
  const [activeTab, setActiveTab] = useState<TabId>('profitability')
  const [range, setRange] = useState<DateRange>(defaultRange)
  // Pre-seeded from a `?branchId=` deep link (e.g. the Branches page's "Reports" action) - once
  // set, the effect below's `if (branchFilter) return` guard leaves it alone.
  const [branchFilter, setBranchFilter] = useState(() => searchParams.get('branchId') ?? '')

  useEffect(() => {
    if (branchFilter) return
    if (canSwitchBranches) setBranchFilter('all')
    else if (activeBranchId) setBranchFilter(activeBranchId)
  }, [canSwitchBranches, activeBranchId, branchFilter])

  const branchId = branchFilter === 'all' || !branchFilter ? undefined : branchFilter

  return (
    <div className={`mx-auto max-w-6xl ${CHART_VARS}`}>
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Reports</h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Real numbers from your sales, expenses, and inventory.
        </p>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <DateRangePicker value={range} onChange={setRange} />
        {canSwitchBranches && (
          <select
            value={branchFilter}
            onChange={(e) => setBranchFilter(e.target.value)}
            className="h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500/40 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="all">All branches</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="mb-6 flex gap-1 border-b border-slate-200 dark:border-slate-800">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-1.5 border-b-2 px-3 py-2.5 text-sm font-medium transition-colors ${
              activeTab === tab.id
                ? 'border-primary-600 text-primary-700 dark:text-primary-400'
                : 'border-transparent text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200'
            }`}
          >
            <tab.icon className="h-4 w-4" />
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'profitability' && <ProfitabilityTab range={range} branchId={branchId} />}
      {activeTab === 'expenses' && <ExpensesTab range={range} branchId={branchId} />}
      {activeTab === 'inventory' && <InventoryTab range={range} branchId={branchId} />}
    </div>
  )
}
