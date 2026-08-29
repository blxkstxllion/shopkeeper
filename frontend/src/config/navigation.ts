import {
  LayoutDashboard,
  ShoppingCart,
  Package,
  Building2,
  Users,
  Truck,
  UserCircle,
  BarChart3,
  Receipt,
  Settings,
  Sparkles,
  History,
  type LucideIcon,
} from 'lucide-react'

export interface NavItem {
  label: string
  to: string
  icon: LucideIcon
  /** Shown in the mobile bottom nav (max 5 items incl. "More"). */
  mobilePriority?: boolean
  /** data-tour hook for the guided first-time tour (see features/tour) - only set on the items the tour spotlights. */
  tourId?: string
}

export const navItems: NavItem[] = [
  { label: 'Dashboard', to: '/app', icon: LayoutDashboard, mobilePriority: true, tourId: 'nav-dashboard' },
  { label: 'Sell', to: '/app/sell', icon: ShoppingCart, mobilePriority: true, tourId: 'nav-sell' },
  { label: 'Sales', to: '/app/sales', icon: Receipt },
  { label: 'Inventory', to: '/app/inventory', icon: Package, mobilePriority: true, tourId: 'nav-inventory' },
  { label: 'AI Advisor', to: '/app/ai', icon: Sparkles, mobilePriority: true, tourId: 'nav-ai' },
  { label: 'Branches', to: '/app/branches', icon: Building2 },
  { label: 'Employees', to: '/app/employees', icon: Users },
  { label: 'Suppliers', to: '/app/suppliers', icon: Truck },
  { label: 'Customers', to: '/app/customers', icon: UserCircle },
  { label: 'Expenses', to: '/app/expenses', icon: Receipt },
  { label: 'Reports', to: '/app/reports', icon: BarChart3, tourId: 'nav-reports' },
  { label: 'Audit Logs', to: '/app/audit-logs', icon: History },
  { label: 'Settings', to: '/app/settings', icon: Settings },
]
