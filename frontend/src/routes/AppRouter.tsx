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
import { ExpensesPage } from '@/features/expenses/ExpensesPage'
import { ReportsPage } from '@/features/reports/ReportsPage'
import { BranchesPage } from '@/features/branches/BranchesPage'
import { SuppliersPage } from '@/features/suppliers/SuppliersPage'
import { CustomersPage } from '@/features/customers/CustomersPage'
import { EmployeesPage } from '@/features/employees/EmployeesPage'
import { AcceptInvitePage } from '@/features/auth/AcceptInvitePage'
import { JoinPage } from '@/features/auth/JoinPage'
import { VerifyEmailPage } from '@/features/auth/VerifyEmailPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { BillingCallbackPage } from '@/features/settings/BillingCallbackPage'
import { AuditLogsPage } from '@/features/audit-logs/AuditLogsPage'
import { AdvisorPage } from '@/features/advisor/AdvisorPage'
import { RequireActiveBusiness, RequireAuth, RedirectIfAuthed, RequirePermission } from './guards'

export function AppRouter() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />

      <Route
        path="/login"
        element={
          <RedirectIfAuthed>
            <LoginPage />
          </RedirectIfAuthed>
        }
      />

      <Route path="/accept-invite" element={<AcceptInvitePage />} />
      <Route path="/join" element={<JoinPage />} />
      <Route path="/verify-email" element={<VerifyEmailPage />} />

      <Route
        element={
          <RedirectIfAuthed>
            <AuthLayout />
          </RedirectIfAuthed>
        }
      >
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
      </Route>

      <Route
        path="/onboarding"
        element={
          <RequireAuth>
            <OnboardingPage />
          </RequireAuth>
        }
      />
      <Route
        path="/select-business"
        element={
          <RequireAuth>
            <SelectBusinessPage />
          </RequireAuth>
        }
      />

      <Route
        element={
          <RequireActiveBusiness>
            <AppLayout />
          </RequireActiveBusiness>
        }
      >
        <Route path="/app" element={<DashboardPage />} />
        <Route path="/app/sell" element={<PosPage />} />
        <Route path="/app/sales" element={<SalesHistoryPage />} />
        <Route path="/app/inventory" element={<InventoryPage />} />
        <Route
          path="/app/ai"
          element={
            <RequirePermission permission="ai_consultant:use">
              <AdvisorPage />
            </RequirePermission>
          }
        />
        <Route path="/app/branches" element={<BranchesPage />} />
        <Route path="/app/employees" element={<EmployeesPage />} />
        <Route path="/app/suppliers" element={<SuppliersPage />} />
        <Route path="/app/customers" element={<CustomersPage />} />
        <Route path="/app/expenses" element={<ExpensesPage />} />
        <Route path="/app/reports" element={<ReportsPage />} />
        <Route
          path="/app/audit-logs"
          element={
            <RequirePermission permission="audit_logs:view">
              <AuditLogsPage />
            </RequirePermission>
          }
        />
        <Route path="/app/settings" element={<SettingsPage />} />
        <Route path="/app/billing/callback" element={<BillingCallbackPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
