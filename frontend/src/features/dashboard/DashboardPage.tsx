import { useQuery } from '@tanstack/react-query'
import { TrendingUp, ShoppingCart, Package, Building2 } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { getBranches } from '@/api/branches'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/ui/EmptyState'

const metricPlaceholders = [
  { label: "Today's Sales", icon: ShoppingCart },
  { label: "Today's Profit", icon: TrendingUp },
  { label: 'Inventory Value', icon: Package },
  { label: 'Active Branches', icon: Building2 },
]

export function DashboardPage() {
  const { user, activeBusiness } = useAuth()
  const { data: branches } = useQuery({ queryKey: ['branches'], queryFn: getBranches })

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6">
        <h1 className="text-xl font-semibold text-slate-900 dark:text-slate-100">
          Welcome back, {user?.firstName}
        </h1>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Here&apos;s what&apos;s happening at {activeBusiness?.businessName}.
        </p>
      </div>

      <div className="mb-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        {metricPlaceholders.map((m) => (
          <Card key={m.label} className="p-4">
            <div className="mb-2 flex items-center gap-2 text-slate-400">
              <m.icon className="h-4 w-4" />
              <span className="text-xs font-medium uppercase tracking-wide">{m.label}</span>
            </div>
            <p className="text-lg font-semibold text-slate-400 dark:text-slate-600">No data yet</p>
          </Card>
        ))}
      </div>

      <Card className="p-4">
        <h2 className="mb-3 text-sm font-semibold text-slate-900 dark:text-slate-100">Branches</h2>
        {branches && branches.length > 0 ? (
          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {branches.map((b) => (
              <div key={b.id} className="flex items-center justify-between py-2.5">
                <div>
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    {b.name}
                    {b.isMainBranch && (
                      <span className="ml-2 rounded-full bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 dark:bg-primary-900/30 dark:text-primary-300">
                        Main
                      </span>
                    )}
                  </p>
                  {b.city && <p className="text-xs text-slate-500 dark:text-slate-400">{b.city}</p>}
                </div>
                <span className="text-xs text-slate-400">Code: {b.code}</span>
              </div>
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Building2}
            title="No branches yet"
            description="Add a branch to start tracking sales and inventory there."
          />
        )}
      </Card>

      <p className="mt-6 text-center text-xs text-slate-400">
        Sales, profit, and inventory analytics arrive in later build phases once the POS and inventory modules are connected.
      </p>
    </div>
  )
}
