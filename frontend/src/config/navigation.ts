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
  type LucideIcon,
} from 'lucide-react'

export interface NavItem {
  label: string
  to: string
  icon: LucideIcon
  /** Shown in the mobile bottom nav (max 5 items incl. "More"). */
  mobilePriority?: boolean
}

export const navItems: NavItem[] = [
  { label: 'Dashboard', to: '/app', icon: LayoutDashboard, mobilePriority: true },
  { label: 'Sell', to: '/app/sell', icon: ShoppingCart, mobilePriority: true },
  { label: 'Inventory', to: '/app/inventory', icon: Package, mobilePriority: true },
  { label: 'AI Advisor', to: '/app/ai', icon: Sparkles, mobilePriority: true },
  { label: 'Branches', to: '/app/branches', icon: Building2 },
  { label: 'Employees', to: '/app/employees', icon: Users },
  { label: 'Suppliers', to: '/app/suppliers', icon: Truck },
  { label: 'Customers', to: '/app/customers', icon: UserCircle },
  { label: 'Expenses', to: '/app/expenses', icon: Receipt },
  { label: 'Reports', to: '/app/reports', icon: BarChart3 },
  { label: 'Settings', to: '/app/settings', icon: Settings },
]
