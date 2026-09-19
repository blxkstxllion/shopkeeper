import type { QueryClient } from '@tanstack/react-query'
import * as productsApi from '@/api/products'
import * as inventoryApi from '@/api/inventory'
import * as customersApi from '@/api/customers'
import * as suppliersApi from '@/api/suppliers'
import * as expensesApi from '@/api/expenses'
import * as employeesApi from '@/api/employees'
import * as rolesApi from '@/api/roles'
import * as branchesApi from '@/api/branches'
import * as businessSettingsApi from '@/api/businessSettings'
import * as aboutApi from '@/api/about'
import * as salesApi from '@/api/sales'
import { cacheList, cacheSingleton, getCachedList, getCachedSingleton } from './cache'
import type { CreateProductPayload, PagedResult, Product, ProductCategory, UpdateProductPayload } from '@/types/product'
import type { Customer, CreateCustomerPayload, UpdateCustomerPayload } from '@/types/customer'
import type {
  CreateSupplierPayload,
  RestockFromSupplierPayload,
  Supplier,
  UpdateSupplierPayload,
} from '@/types/supplier'
import type { CreateExpensePayload, Expense, ExpenseCategory, UpdateExpensePayload } from '@/types/expense'
import type { InviteEmployeePayload } from '@/types/employee'
import type { RolePayload } from '@/types/role'
import type { Branch, CreateBranchPayload, UpdateBranchPayload } from '@/types/business'
import type { UpdateBusinessProfilePayload, UpdateTaxSettingsPayload } from '@/types/businessSettings'
import type { UpdateBusinessAboutPayload } from '@/types/about'
import type { AdjustStockPayload } from '@/api/inventory'
import type { CreateSalePayload } from '@/types/sale'
import type { OfflineEntityType } from './db'

export interface OptimisticInsertContext {
  businessId: string
  clientRequestId: string
  userName: string
}

export interface MutationDefinition<TPayload = unknown> {
  call: (payload: TPayload, clientRequestId: string) => Promise<unknown>
  /** Which cached queries to refresh after a successful sync - deliberately not "invalidate
   * everything," since that would refetch data the user isn't even looking at right now. */
  invalidate: (queryClient: QueryClient) => void | Promise<void>
  /** Only for "create" mutations whose list the user is looking at right now - synthesizes a
   * plausible row from the payload alone and writes it straight into that list's offline
   * cache, so a queued create shows up immediately instead of only existing in the sync-queue
   * popup until it actually reaches the server. The synthesized row (id prefixed `queued-`) is
   * a stand-in only: the next successful online fetch for that list REPLACES the whole cached
   * list wholesale (see cacheList/cacheSingleton), so it's automatically swapped for the real,
   * server-assigned row once sync succeeds - no separate cleanup needed. Deliberately not
   * implemented for every entity type - anything whose row needs a field this client can't
   * safely fabricate (employee invites/join requests resolve role/permission display
   * server-side; roles similarly) is left out on purpose rather than guessed at. */
  optimisticInsert?: (payload: TPayload, ctx: OptimisticInsertContext) => Promise<void>
  /** Same idea as optimisticInsert but for a delete/deactivate action on an EXISTING cached
   * row - without this, clicking "Deactivate" while offline gave no visible feedback at all
   * (the row just sat there unchanged until it actually synced), which is exactly what led a
   * real user to click the same button 14 times thinking nothing had happened, queuing 14
   * duplicate mutations for one action. Patching the row immediately means the button that
   * triggered this (gated on `isActive` in every list page) stops rendering the moment it's
   * clicked once, so it's structurally impossible to double-fire from the same click target. */
  optimisticDelete?: (payload: TPayload, ctx: { businessId: string }) => Promise<void>
}

async function invalidateKeys(queryClient: QueryClient, keys: string[][]) {
  await Promise.all(keys.map((key) => queryClient.invalidateQueries({ queryKey: key })))
}

async function deactivateCachedItem<T extends { id: string; isActive: boolean }>(
  store: 'branches' | 'customers' | 'suppliers',
  businessId: string,
  id: string,
) {
  const existing = await getCachedList<T>(store, businessId)
  await cacheList(
    store,
    businessId,
    existing.map((item) => (item.id === id ? { ...item, isActive: false } : item)),
  )
}

export const mutationRegistry: Record<OfflineEntityType, MutationDefinition<never>> = {
  sale: {
    call: (payload, clientRequestId) => salesApi.createSale({ ...(payload as CreateSalePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['sellable-products'], ['sales'], ['dashboard']]),
  },
  refund: {
    call: (payload, clientRequestId) => {
      const { saleId, ...rest } = payload as {
        saleId: string
        items: { saleItemId: string; quantity: number }[]
        reason: string
      }
      return salesApi.refundSale(saleId, { ...rest, clientRequestId })
    },
    invalidate: (qc) => invalidateKeys(qc, [['sales'], ['dashboard']]),
  },
  void: {
    call: (payload, clientRequestId) => {
      const { saleId, reason } = payload as { saleId: string; reason: string }
      return salesApi.voidSale(saleId, reason, clientRequestId)
    },
    invalidate: (qc) => invalidateKeys(qc, [['sales'], ['dashboard']]),
  },
  product: {
    call: (payload, clientRequestId) =>
      productsApi.createProduct({ ...(payload as CreateProductPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['products'], ['sellable-products'], ['inventory-stats']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as CreateProductPayload
      const singletonKey = `products:${p.branchId ?? 'all'}`
      const [cached, categories, suppliers] = await Promise.all([
        getCachedSingleton<PagedResult<Product>>(singletonKey, ctx.businessId),
        getCachedList<ProductCategory>('categories', ctx.businessId),
        getCachedList<Supplier>('suppliers', ctx.businessId),
      ])
      if (!cached) return // nothing cached for this branch yet - the next real fetch populates it normally
      const optimistic: Product = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        sku: p.sku,
        barcode: p.barcode ?? null,
        description: p.description ?? null,
        imageUrl: p.imageUrl ?? null,
        categoryId: p.categoryId ?? null,
        categoryName: categories.find((c) => c.id === p.categoryId)?.name ?? null,
        supplierId: p.supplierId ?? null,
        supplierName: suppliers.find((s) => s.id === p.supplierId)?.name ?? null,
        sellingPrice: p.sellingPrice,
        costPrice: p.costPrice,
        minimumStock: p.minimumStock,
        trackInventory: p.trackInventory,
        isActive: true,
        quantityOnHand: p.trackInventory ? p.initialQuantity : null,
        isLowStock: p.trackInventory ? p.initialQuantity < p.minimumStock : false,
      }
      await cacheSingleton(singletonKey, ctx.businessId, {
        ...cached,
        items: [optimistic, ...cached.items],
        totalCount: cached.totalCount + 1,
      })
    },
  },
  productUpdate: {
    call: (payload, clientRequestId) =>
      productsApi.updateProduct({ ...(payload as UpdateProductPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['products'], ['sellable-products']]),
  },
  productDelete: {
    call: (payload, clientRequestId) => productsApi.deleteProduct((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['products'], ['sellable-products']]),
  },
  productCategory: {
    call: (payload, clientRequestId) =>
      productsApi.createProductCategory({
        ...(payload as { name: string; description?: string | null }),
        clientRequestId,
      }),
    invalidate: (qc) => invalidateKeys(qc, [['product-categories']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as { name: string; description?: string | null }
      const existing = await getCachedList<ProductCategory>('categories', ctx.businessId)
      const optimistic: ProductCategory = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        description: p.description ?? null,
        isActive: true,
      }
      await cacheList('categories', ctx.businessId, [...existing, optimistic])
    },
  },
  stockAdjustment: {
    call: (payload, clientRequestId) =>
      inventoryApi.adjustStock({ ...(payload as AdjustStockPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['products'], ['inventory-stats'], ['inventory-transactions']]),
  },
  customer: {
    call: (payload, clientRequestId) =>
      customersApi.createCustomer({ ...(payload as CreateCustomerPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['customers']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as CreateCustomerPayload
      const existing = await getCachedList<Customer>('customers', ctx.businessId)
      const optimistic: Customer = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        phone: p.phone ?? null,
        email: p.email ?? null,
        address: p.address ?? null,
        isActive: true,
      }
      await cacheList('customers', ctx.businessId, [...existing, optimistic])
    },
  },
  customerUpdate: {
    call: (payload, clientRequestId) =>
      customersApi.updateCustomer({ ...(payload as UpdateCustomerPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['customers']]),
  },
  customerDelete: {
    call: (payload, clientRequestId) => customersApi.deleteCustomer((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['customers']]),
    optimisticDelete: (payload, ctx) =>
      deactivateCachedItem<Customer>('customers', ctx.businessId, (payload as { id: string }).id),
  },
  supplier: {
    call: (payload, clientRequestId) =>
      suppliersApi.createSupplier({ ...(payload as CreateSupplierPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as CreateSupplierPayload
      const existing = await getCachedList<Supplier>('suppliers', ctx.businessId)
      const optimistic: Supplier = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        contactName: p.contactName ?? null,
        phone: p.phone ?? null,
        email: p.email ?? null,
        address: p.address ?? null,
        isActive: true,
      }
      await cacheList('suppliers', ctx.businessId, [...existing, optimistic])
    },
  },
  supplierUpdate: {
    call: (payload, clientRequestId) =>
      suppliersApi.updateSupplier({ ...(payload as UpdateSupplierPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
  },
  supplierDelete: {
    call: (payload, clientRequestId) => suppliersApi.deleteSupplier((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
    optimisticDelete: (payload, ctx) =>
      deactivateCachedItem<Supplier>('suppliers', ctx.businessId, (payload as { id: string }).id),
  },
  restock: {
    call: (payload, clientRequestId) => {
      const { supplierId, ...rest } = payload as { supplierId: string } & RestockFromSupplierPayload
      return suppliersApi.restockFromSupplier(supplierId, { ...rest, clientRequestId })
    },
    invalidate: (qc) => invalidateKeys(qc, [['products'], ['inventory-stats'], ['suppliers']]),
  },
  expense: {
    call: (payload, clientRequestId) =>
      expensesApi.createExpense({ ...(payload as CreateExpensePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['expenses']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as CreateExpensePayload
      const [existing, categories, branches] = await Promise.all([
        getCachedList<Expense>('expenses', ctx.businessId),
        getCachedList<ExpenseCategory>('expenseCategories', ctx.businessId),
        getCachedList<Branch>('branches', ctx.businessId),
      ])
      const optimistic: Expense = {
        id: `queued-${ctx.clientRequestId}`,
        branchId: p.branchId ?? null,
        branchName: p.branchId ? (branches.find((b) => b.id === p.branchId)?.name ?? null) : null,
        expenseCategoryId: p.expenseCategoryId,
        categoryName: categories.find((c) => c.id === p.expenseCategoryId)?.name ?? 'Uncategorized',
        amount: p.amount,
        expenseDate: p.expenseDate,
        description: p.description ?? null,
        createdByName: ctx.userName,
        createdAt: new Date().toISOString(),
      }
      await cacheList('expenses', ctx.businessId, [optimistic, ...existing])
    },
  },
  expenseUpdate: {
    call: (payload, clientRequestId) =>
      expensesApi.updateExpense({ ...(payload as UpdateExpensePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['expenses']]),
  },
  expenseDelete: {
    call: (payload, clientRequestId) => expensesApi.deleteExpense((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['expenses']]),
    // Expense has no isActive field (unlike branches/customers/suppliers) - its "Delete"
    // button really does remove the row, so the optimistic version removes it too.
    optimisticDelete: async (payload, ctx) => {
      const { id } = payload as { id: string }
      const existing = await getCachedList<Expense>('expenses', ctx.businessId)
      await cacheList(
        'expenses',
        ctx.businessId,
        existing.filter((e) => e.id !== id),
      )
    },
  },
  expenseCategory: {
    call: (payload, clientRequestId) =>
      expensesApi.createExpenseCategory({
        ...(payload as { name: string; description?: string | null }),
        clientRequestId,
      }),
    invalidate: (qc) => invalidateKeys(qc, [['expense-categories']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as { name: string; description?: string | null }
      const existing = await getCachedList<ExpenseCategory>('expenseCategories', ctx.businessId)
      const optimistic: ExpenseCategory = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        description: p.description ?? null,
        isActive: true,
      }
      await cacheList('expenseCategories', ctx.businessId, [...existing, optimistic])
    },
  },
  employeeInvite: {
    call: (payload, clientRequestId) =>
      employeesApi.inviteEmployee({ ...(payload as InviteEmployeePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
  },
  employeeRemove: {
    call: (payload, clientRequestId) =>
      employeesApi.removeEmployee((payload as { businessUserId: string }).businessUserId, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
  },
  joinRequestApprove: {
    call: (payload, clientRequestId) => {
      const { id, ...rest } = payload as { id: string; roleId: string; branchId?: string | null }
      return employeesApi.approveJoinRequest(id, { ...rest, clientRequestId })
    },
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
  },
  joinRequestReject: {
    call: (payload, clientRequestId) => employeesApi.rejectJoinRequest((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
  },
  role: {
    call: (payload, clientRequestId) => rolesApi.createRole({ ...(payload as RolePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
  },
  roleUpdate: {
    call: (payload, clientRequestId) =>
      rolesApi.updateRole({ ...(payload as RolePayload & { id: string }), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
  },
  roleDelete: {
    call: (payload, clientRequestId) => rolesApi.deleteRole((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
  },
  branch: {
    call: (payload, clientRequestId) =>
      branchesApi.createBranch({ ...(payload as CreateBranchPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['branches']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as CreateBranchPayload
      const existing = await getCachedList<Branch>('branches', ctx.businessId)
      const optimistic: Branch = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        code: p.code,
        address: p.address ?? null,
        city: p.city ?? null,
        country: p.country ?? null,
        phone: p.phone ?? null,
        email: p.email ?? null,
        isMainBranch: false,
        isActive: true,
      }
      await cacheList('branches', ctx.businessId, [...existing, optimistic])
    },
  },
  branchUpdate: {
    call: (payload, clientRequestId) =>
      branchesApi.updateBranch({ ...(payload as UpdateBranchPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['branches']]),
  },
  branchDelete: {
    call: (payload, clientRequestId) => branchesApi.deleteBranch((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['branches']]),
    optimisticDelete: (payload, ctx) =>
      deactivateCachedItem<Branch>('branches', ctx.businessId, (payload as { id: string }).id),
  },
  businessProfile: {
    call: (payload, clientRequestId) =>
      businessSettingsApi.updateBusinessProfile({ ...(payload as UpdateBusinessProfilePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['business-settings']]),
  },
  taxSettings: {
    call: (payload, clientRequestId) =>
      businessSettingsApi.updateTaxSettings({ ...(payload as UpdateTaxSettingsPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['business-settings']]),
  },
  businessAbout: {
    call: (payload, clientRequestId) =>
      aboutApi.updateBusinessAbout({ ...(payload as UpdateBusinessAboutPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['business-about']]),
  },
} as Record<OfflineEntityType, MutationDefinition<never>>

// Deliberately NOT offline-eligible: uploading a logo/photo/product image is a two-step flow
// (upload the file, get back a URL, then attach that URL to a separate form's payload) - the
// second step can't be queued meaningfully before the first has actually run and produced a
// real URL. Each upload site disables its own upload button while offline instead
// (AboutPage, ProductFormModal, DashboardHeader) rather than queuing something that can't
// complete correctly. Revisit only with a real two-phase design (queue the upload, then a
// second queued step that patches the owning record once the URL exists).
