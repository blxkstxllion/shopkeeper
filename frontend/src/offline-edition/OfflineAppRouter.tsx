import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/layouts/AppLayout'
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
import { AuditLogsPage } from '@/features/audit-logs/AuditLogsPage'
import { AdvisorPage } from '@/features/advisor/AdvisorPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { AboutPage } from '@/features/about/AboutPage'
import { OfflineSyncProvider } from '@/offline/OfflineSyncContext'
import { RequirePermission } from '@/routes/guards'
import { useLocalAuth } from './LocalAuthContext'
import { OfflineAuthBridge } from './OfflineAuthBridge'
import { SplashScreen } from './SplashScreen'
import { SetupWizardPage } from './SetupWizardPage'
import { PinLockScreen } from './PinLockScreen'

/**
 * No login, no JWT, no /register|/forgot-password|/reset-password|/accept-invite|/join|
 * /verify-email|/select-business|/onboarding. AI Advisor DOES stay, same route as the SaaS
 * build - no Anthropic key is ever configured here, but AdvisorPage already degrades to a
 * deterministic, template-based "quick questions" advisor with zero AI dependency (see
 * UnavailableAdvisorConversationClient) - not "real AI", still useful. Employees/Audit Logs
 * stay routable too (just dropped from nav, see config/navigation.ts) since there's nothing
 * unsafe about reaching them, just nothing useful to do there with a single owner and no team.
 */
export function OfflineAppRouter() {
  const { phase } = useLocalAuth()

  if (phase === 'starting' || phase === 'backend-unreachable') return <SplashScreen />
  if (phase === 'needs-setup') return <SetupWizardPage />
  if (phase === 'locked') return <PinLockScreen />

  return (
    <OfflineAuthBridge>
      {/* Nested here, not in App.tsx's OfflineAppTree - useSyncQueue (which this runs) calls
          useAuth() directly, so it can only mount once OfflineAuthBridge above has actually
          provided a real AuthContextValue. Still required even though nothing is ever queued:
          AppLayout's (reused unmodified) TopNav renders OfflineStatusIndicator, which also
          calls useOfflineSync() unconditionally. */}
      <OfflineSyncProvider>
        <Routes>
          <Route path="/" element={<Navigate to="/app" replace />} />
          <Route element={<AppLayout />}>
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
            <Route path="/app/audit-logs" element={<AuditLogsPage />} />
            <Route path="/app/settings" element={<SettingsPage />} />
            <Route path="/app/about" element={<AboutPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/app" replace />} />
        </Routes>
      </OfflineSyncProvider>
    </OfflineAuthBridge>
  )
}
