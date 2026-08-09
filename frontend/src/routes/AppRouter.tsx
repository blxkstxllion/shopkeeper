import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthLayout } from '@/layouts/AuthLayout'
import { AppLayout } from '@/layouts/AppLayout'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { ForgotPasswordPage } from '@/features/auth/ForgotPasswordPage'
import { ResetPasswordPage } from '@/features/auth/ResetPasswordPage'
import { SelectBusinessPage } from '@/features/auth/SelectBusinessPage'
import { OnboardingPage } from '@/features/onboarding/OnboardingPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { PosPage } from '@/features/pos/PosPage'
import { InventoryPage } from '@/features/inventory/InventoryPage'
import { SalesHistoryPage } from '@/features/sales/SalesHistoryPage'
import { ComingSoon } from '@/components/ComingSoon'
import { RequireActiveBusiness, RequireAuth, RedirectIfAuthed } from './guards'
import { Building2, Users, Truck, UserCircle, Receipt, BarChart3, Settings, Sparkles } from 'lucide-react'

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />

      <Route element={<RedirectIfAuthed><AuthLayout /></RedirectIfAuthed>}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
      </Route>

      <Route path="/onboarding" element={<RequireAuth><OnboardingPage /></RequireAuth>} />
      <Route path="/select-business" element={<RequireAuth><SelectBusinessPage /></RequireAuth>} />

      <Route element={<RequireActiveBusiness><AppLayout /></RequireActiveBusiness>}>
        <Route path="/app" element={<DashboardPage />} />
        <Route path="/app/sell" element={<PosPage />} />
        <Route path="/app/sales" element={<SalesHistoryPage />} />
        <Route path="/app/inventory" element={<InventoryPage />} />
        <Route path="/app/ai" element={<ComingSoon title="The Shop Keeper Advisor" icon={Sparkles} phase="Phase 6" />} />
        <Route path="/app/branches" element={<ComingSoon title="Branch management" icon={Building2} phase="Phase 4" />} />
        <Route path="/app/employees" element={<ComingSoon title="Employees" icon={Users} phase="Phase 4" />} />
        <Route path="/app/suppliers" element={<ComingSoon title="Suppliers" icon={Truck} phase="Phase 4" />} />
        <Route path="/app/customers" element={<ComingSoon title="Customers" icon={UserCircle} phase="Phase 4" />} />
        <Route path="/app/expenses" element={<ComingSoon title="Expenses" icon={Receipt} phase="Phase 3" />} />
        <Route path="/app/reports" element={<ComingSoon title="Reports" icon={BarChart3} phase="Phase 3" />} />
        <Route path="/app/settings" element={<ComingSoon title="Settings" icon={Settings} phase="a later phase" />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
