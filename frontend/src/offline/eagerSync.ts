import * as branchesApi from '@/api/branches'
import * as productsApi from '@/api/products'
import * as customersApi from '@/api/customers'
import * as suppliersApi from '@/api/suppliers'
import * as expensesApi from '@/api/expenses'
import * as inventoryApi from '@/api/inventory'
import * as rolesApi from '@/api/roles'
import * as businessSettingsApi from '@/api/businessSettings'
import * as dashboardApi from '@/api/dashboard'
import * as reportsApi from '@/api/reports'
import { cacheList, cacheSingleton } from './cache'
import { cacheImageBlob } from './imageCache'
import { resolveUploadUrl } from '@/lib/format'
import type { Branch } from '@/types/business'
import type { ReportParams } from '@/api/reports'

function defaultReportRange(): { from: string; to: string } {
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 29)
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) }
}

/** Singleton key for a report tab's default (last-30-days, no comparison) view - shared with
 * the tab components themselves (ProfitabilityTab/ExpensesTab/InventoryTab) so a cache entry
 * written here is the same one those screens read from offline. Only the default range is
 * eager-synced (see the product proposal this implements) - a custom date range picked later
 * is still only available offline if it happens to have been viewed live at some point,
 * exactly like every other lazily-cached screen. */
export function reportSingletonKey(
  report: 'Profitability' | 'Expenses' | 'Inventory',
  branchId: string | undefined,
  range: { from: string; to: string },
): string {
  return `reports${report}:${branchId ?? 'all'}:${range.from}:${range.to}`
}

async function syncBranchScoped(businessId: string, branchId: string | undefined, range: { from: string; to: string }) {
  const params: ReportParams = { ...range, branchId }
  const [productsPage, dashboard, stats, profitability, expenses, inventory] = await Promise.allSettled([
    productsApi.getProducts({ branchId, activeOnly: true, page: 1, pageSize: 200 }),
    dashboardApi.getDashboardSummary(branchId),
    inventoryApi.getInventoryStats(branchId),
    reportsApi.getProfitabilityReport(params),
    reportsApi.getExpenseReport(params),
    reportsApi.getInventoryReport(params),
  ])

  if (productsPage.status === 'fulfilled') {
    await cacheSingleton(`products:${branchId ?? 'all'}`, businessId, productsPage.value)
    await Promise.allSettled(
      productsPage.value.items
        .filter((p) => Boolean(p.imageUrl))
        .map((p) => cacheImageBlob(resolveUploadUrl(p.imageUrl!), businessId)),
    )
  }
  if (dashboard.status === 'fulfilled') {
    await cacheSingleton(`dashboardSummary:${branchId ?? 'all'}`, businessId, dashboard.value)
  }
  if (stats.status === 'fulfilled') {
    await cacheSingleton(`inventoryStats:${branchId ?? 'all'}`, businessId, stats.value)
  }
  if (profitability.status === 'fulfilled') {
    await cacheSingleton(reportSingletonKey('Profitability', branchId, range), businessId, profitability.value)
  }
  if (expenses.status === 'fulfilled') {
    await cacheSingleton(reportSingletonKey('Expenses', branchId, range), businessId, expenses.value)
  }
  if (inventory.status === 'fulfilled') {
    await cacheSingleton(reportSingletonKey('Inventory', branchId, range), businessId, inventory.value)
  }
}

/** Runs once right after a fresh ONLINE login/business-selection - proactively pulls the data
 * set the product decided should always be available offline (branches, products+photos,
 * customers, suppliers, categories, roles, business settings+logo, dashboard, and each
 * report's default view) instead of waiting for the user to visit every screen first while
 * still connected. Best-effort and non-blocking throughout (Promise.allSettled at every
 * level): one failing request never stops the rest, and the caller doesn't await this before
 * letting the user into the app - it's a background warm-up, not a login gate. */
export async function runEagerSync(businessId: string, userPhotoUrl?: string | null): Promise<void> {
  if (userPhotoUrl) void cacheImageBlob(resolveUploadUrl(userPhotoUrl), businessId)

  const [branchesResult, categories, expenseCategories, customersPage, suppliers, roles, roleManagement, settings] =
    await Promise.allSettled([
      branchesApi.getBranches(),
      productsApi.getProductCategories(),
      expensesApi.getExpenseCategories(),
      customersApi.getCustomers({ activeOnly: true, pageSize: 200 }),
      suppliersApi.getSuppliers(),
      rolesApi.getRoles(),
      rolesApi.getRoleManagement(),
      businessSettingsApi.getBusinessSettings(),
    ])

  if (categories.status === 'fulfilled') await cacheList('categories', businessId, categories.value)
  if (expenseCategories.status === 'fulfilled') {
    await cacheList('expenseCategories', businessId, expenseCategories.value)
  }
  if (customersPage.status === 'fulfilled') await cacheList('customers', businessId, customersPage.value.items)
  if (suppliers.status === 'fulfilled') await cacheList('suppliers', businessId, suppliers.value)
  if (roles.status === 'fulfilled') await cacheList('roles', businessId, roles.value)
  if (roleManagement.status === 'fulfilled') {
    await cacheSingleton('roleManagement', businessId, roleManagement.value)
  }
  if (settings.status === 'fulfilled') {
    await cacheSingleton('businessSettings', businessId, settings.value)
    if (settings.value.logoUrl) void cacheImageBlob(resolveUploadUrl(settings.value.logoUrl), businessId)
  }

  if (branchesResult.status !== 'fulfilled') return
  const branches: Branch[] = branchesResult.value
  await cacheList('branches', businessId, branches)

  const range = defaultReportRange()
  await Promise.allSettled([
    // Per-branch snapshots, so switching branches offline (see BranchContext) still finds
    // something cached rather than only whichever branch happened to be active at login.
    ...branches.map((b) => syncBranchScoped(businessId, b.id, range)),
    // Plus the unfiltered "all branches" view Dashboard/Reports default to for owners/admins
    // who can switch between branches.
    syncBranchScoped(businessId, undefined, range),
  ])
}
