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
import type {
  BusinessMember,
  BusinessUsersResponse,
  InviteEmployeePayload,
  PendingInvitationItem,
  Role,
} from '@/types/employee'
import type { RoleManagement, RolePayload } from '@/types/role'
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
  /** Same idea as optimisticInsert but patches the fields of an EXISTING cached row - an edit
   * made offline (e.g. a customer's phone number) otherwise wouldn't show until it actually
   * synced, same gap optimisticDelete closes for deletes/deactivates. */
  optimisticUpdate?: (payload: TPayload, ctx: OptimisticInsertContext) => Promise<void>
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

async function updateCachedItem<T extends { id: string }>(
  store: 'branches' | 'customers' | 'suppliers' | 'expenses',
  businessId: string,
  id: string,
  patch: Partial<T>,
) {
  const existing = await getCachedList<T>(store, businessId)
  await cacheList(
    store,
    businessId,
    existing.map((item) => (item.id === id ? { ...item, ...patch } : item)),
  )
}

/** Same idea as updateCachedItem/cacheList, but for the singleton-cached array shapes
 * (RoleManagement[], BusinessUsersResponse.members/pendingInvitations/joinRequests) instead of
 * a plain OfflineListStore - those go through cacheSingleton/getCachedSingleton, not
 * cacheList/getCachedList. */
async function updateCachedSingletonArrayItem<T extends { id: string }>(
  key: string,
  businessId: string,
  id: string,
  patch: Partial<T>,
) {
  const existing = await getCachedSingleton<T[]>(key, businessId)
  if (!existing) return
  await cacheSingleton(
    key,
    businessId,
    existing.map((item) => (item.id === id ? { ...item, ...patch } : item)),
  )
}

async function removeCachedSingletonArrayItem<T extends { id: string }>(key: string, businessId: string, id: string) {
  const existing = await getCachedSingleton<T[]>(key, businessId)
  if (!existing) return
  await cacheSingleton(
    key,
    businessId,
    existing.filter((item) => item.id !== id),
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
    optimisticUpdate: async (payload, ctx) => {
      const p = payload as UpdateProductPayload
      const singletonKey = `products:${p.branchId ?? 'all'}`
      const [cached, categories, suppliers] = await Promise.all([
        getCachedSingleton<PagedResult<Product>>(singletonKey, ctx.businessId),
        getCachedList<ProductCategory>('categories', ctx.businessId),
        getCachedList<Supplier>('suppliers', ctx.businessId),
      ])
      if (!cached) return
      await cacheSingleton(singletonKey, ctx.businessId, {
        ...cached,
        items: cached.items.map((item) =>
          item.id === p.id
            ? {
                ...item,
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
                isActive: p.isActive,
                // quantityOnHand/isLowStock deliberately untouched - an edit here doesn't
                // change stock, that's stockAdjustment's job.
              }
            : item,
        ),
      })
    },
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
    optimisticUpdate: (payload, ctx) => {
      const p = payload as UpdateCustomerPayload
      return updateCachedItem<Customer>('customers', ctx.businessId, p.id, {
        name: p.name,
        phone: p.phone ?? null,
        email: p.email ?? null,
        address: p.address ?? null,
        isActive: p.isActive,
      })
    },
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
    optimisticUpdate: (payload, ctx) => {
      const p = payload as UpdateSupplierPayload
      return updateCachedItem<Supplier>('suppliers', ctx.businessId, p.id, {
        name: p.name,
        contactName: p.contactName ?? null,
        phone: p.phone ?? null,
        email: p.email ?? null,
        address: p.address ?? null,
        isActive: p.isActive,
      })
    },
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
    optimisticUpdate: async (payload, ctx) => {
      const p = payload as UpdateExpensePayload
      const [categories, branches] = await Promise.all([
        getCachedList<ExpenseCategory>('expenseCategories', ctx.businessId),
        getCachedList<Branch>('branches', ctx.businessId),
      ])
      await updateCachedItem<Expense>('expenses', ctx.businessId, p.id, {
        branchId: p.branchId ?? null,
        branchName: p.branchId ? (branches.find((b) => b.id === p.branchId)?.name ?? null) : null,
        expenseCategoryId: p.expenseCategoryId,
        categoryName: categories.find((c) => c.id === p.expenseCategoryId)?.name ?? 'Uncategorized',
        amount: p.amount,
        expenseDate: p.expenseDate,
        description: p.description ?? null,
      })
    },
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
    optimisticInsert: async (payload, ctx) => {
      const p = payload as InviteEmployeePayload
      const [cached, roles, branches] = await Promise.all([
        getCachedSingleton<BusinessUsersResponse>('businessUsers', ctx.businessId),
        getCachedList<Role>('roles', ctx.businessId),
        getCachedList<Branch>('branches', ctx.businessId),
      ])
      if (!cached) return
      const optimistic: PendingInvitationItem = {
        id: `queued-${ctx.clientRequestId}`,
        email: p.email,
        roleName: roles.find((r) => r.id === p.roleId)?.name ?? 'Unknown role',
        branchName: p.branchId ? (branches.find((b) => b.id === p.branchId)?.name ?? null) : null,
        invitedAt: new Date().toISOString(),
        // Matches InviteEmployeeCommand's ExpiresAt = UtcNow.AddDays(7) - the real invite's
        // actual expiry once synced, this is just a plausible stand-in in the meantime.
        expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      }
      await cacheSingleton('businessUsers', ctx.businessId, {
        ...cached,
        pendingInvitations: [...cached.pendingInvitations, optimistic],
      })
    },
  },
  employeeRemove: {
    call: (payload, clientRequestId) =>
      employeesApi.removeEmployee((payload as { businessUserId: string }).businessUserId, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
    // The backend query excludes Removed members entirely (GetBusinessUsersQuery filters
    // Status != Removed), so the optimistic version removes the row rather than tagging its
    // status - matching what the real list will look like once this actually syncs.
    optimisticDelete: async (payload, ctx) => {
      const { businessUserId } = payload as { businessUserId: string }
      const cached = await getCachedSingleton<BusinessUsersResponse>('businessUsers', ctx.businessId)
      if (!cached) return
      await cacheSingleton('businessUsers', ctx.businessId, {
        ...cached,
        members: cached.members.filter((m) => m.businessUserId !== businessUserId),
      })
    },
  },
  joinRequestApprove: {
    call: (payload, clientRequestId) => {
      const { id, ...rest } = payload as { id: string; roleId: string; branchId?: string | null }
      return employeesApi.approveJoinRequest(id, { ...rest, clientRequestId })
    },
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
    // Approving a join request is really two changes at once - the request disappears from
    // joinRequests AND a new member appears in members. optimisticInsert/optimisticDelete both
    // run independently (see useOfflineMutation), so this does both against the same cache.
    optimisticInsert: async (payload, ctx) => {
      const p = payload as { id: string; roleId: string; branchId?: string | null }
      const [cached, roles, branches] = await Promise.all([
        getCachedSingleton<BusinessUsersResponse>('businessUsers', ctx.businessId),
        getCachedList<Role>('roles', ctx.businessId),
        getCachedList<Branch>('branches', ctx.businessId),
      ])
      if (!cached) return
      const request = cached.joinRequests.find((r) => r.id === p.id)
      if (!request) return
      const optimistic: BusinessMember = {
        businessUserId: `queued-${ctx.clientRequestId}`,
        userId: `queued-${ctx.clientRequestId}`,
        firstName: request.firstName,
        lastName: request.lastName,
        email: request.email,
        roleName: roles.find((r) => r.id === p.roleId)?.name ?? 'Unknown role',
        branchName: p.branchId ? (branches.find((b) => b.id === p.branchId)?.name ?? null) : null,
        status: 'Active',
        isOwner: false,
        joinedAt: new Date().toISOString(),
      }
      await cacheSingleton('businessUsers', ctx.businessId, {
        ...cached,
        members: [...cached.members, optimistic],
      })
    },
    optimisticDelete: async (payload, ctx) => {
      const { id } = payload as { id: string }
      const cached = await getCachedSingleton<BusinessUsersResponse>('businessUsers', ctx.businessId)
      if (!cached) return
      await cacheSingleton('businessUsers', ctx.businessId, {
        ...cached,
        joinRequests: cached.joinRequests.filter((r) => r.id !== id),
      })
    },
  },
  joinRequestReject: {
    call: (payload, clientRequestId) => employeesApi.rejectJoinRequest((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['business-users']]),
    optimisticDelete: async (payload, ctx) => {
      const { id } = payload as { id: string }
      const cached = await getCachedSingleton<BusinessUsersResponse>('businessUsers', ctx.businessId)
      if (!cached) return
      await cacheSingleton('businessUsers', ctx.businessId, {
        ...cached,
        joinRequests: cached.joinRequests.filter((r) => r.id !== id),
      })
    },
  },
  role: {
    call: (payload, clientRequestId) => rolesApi.createRole({ ...(payload as RolePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
    optimisticInsert: async (payload, ctx) => {
      const p = payload as RolePayload
      const existing = await getCachedSingleton<RoleManagement[]>('roleManagement', ctx.businessId)
      if (!existing) return
      const optimistic: RoleManagement = {
        id: `queued-${ctx.clientRequestId}`,
        name: p.name,
        description: p.description,
        isSystemRole: false,
        permissionKeys: p.permissionKeys,
        employeeCount: 0,
      }
      await cacheSingleton('roleManagement', ctx.businessId, [...existing, optimistic])
    },
  },
  roleUpdate: {
    call: (payload, clientRequestId) =>
      rolesApi.updateRole({ ...(payload as RolePayload & { id: string }), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
    optimisticUpdate: (payload, ctx) => {
      const p = payload as RolePayload & { id: string }
      return updateCachedSingletonArrayItem<RoleManagement>('roleManagement', ctx.businessId, p.id, {
        name: p.name,
        description: p.description,
        permissionKeys: p.permissionKeys,
      })
    },
  },
  roleDelete: {
    call: (payload, clientRequestId) => rolesApi.deleteRole((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['roles'], ['role-management']]),
    optimisticDelete: (payload, ctx) =>
      removeCachedSingletonArrayItem<RoleManagement>('roleManagement', ctx.businessId, (payload as { id: string }).id),
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
    optimisticUpdate: (payload, ctx) => {
      const p = payload as UpdateBranchPayload
      return updateCachedItem<Branch>('branches', ctx.businessId, p.id, {
        name: p.name,
        code: p.code,
        address: p.address ?? null,
        city: p.city ?? null,
        country: p.country ?? null,
        phone: p.phone ?? null,
        email: p.email ?? null,
        // Payload field is `isMain`, entity field is `isMainBranch` - not a typo, they're
        // genuinely named differently between the two.
        isMainBranch: p.isMain,
        isActive: p.isActive,
      })
    },
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
