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
import type { CreateProductPayload, UpdateProductPayload } from '@/types/product'
import type { CreateCustomerPayload, UpdateCustomerPayload } from '@/types/customer'
import type { CreateSupplierPayload, RestockFromSupplierPayload, UpdateSupplierPayload } from '@/types/supplier'
import type { CreateExpensePayload, UpdateExpensePayload } from '@/types/expense'
import type { InviteEmployeePayload } from '@/types/employee'
import type { RolePayload } from '@/types/role'
import type { CreateBranchPayload, UpdateBranchPayload } from '@/types/business'
import type { UpdateBusinessProfilePayload, UpdateTaxSettingsPayload } from '@/types/businessSettings'
import type { UpdateBusinessAboutPayload } from '@/types/about'
import type { AdjustStockPayload } from '@/api/inventory'
import type { CreateSalePayload } from '@/types/sale'
import type { OfflineEntityType } from './db'

export interface MutationDefinition<TPayload = unknown> {
  call: (payload: TPayload, clientRequestId: string) => Promise<unknown>
  /** Which cached queries to refresh after a successful sync - deliberately not "invalidate
   * everything," since that would refetch data the user isn't even looking at right now. */
  invalidate: (queryClient: QueryClient) => void | Promise<void>
}

async function invalidateKeys(queryClient: QueryClient, keys: string[][]) {
  await Promise.all(keys.map((key) => queryClient.invalidateQueries({ queryKey: key })))
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
  },
  customerUpdate: {
    call: (payload, clientRequestId) =>
      customersApi.updateCustomer({ ...(payload as UpdateCustomerPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['customers']]),
  },
  customerDelete: {
    call: (payload, clientRequestId) => customersApi.deleteCustomer((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['customers']]),
  },
  supplier: {
    call: (payload, clientRequestId) =>
      suppliersApi.createSupplier({ ...(payload as CreateSupplierPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
  },
  supplierUpdate: {
    call: (payload, clientRequestId) =>
      suppliersApi.updateSupplier({ ...(payload as UpdateSupplierPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
  },
  supplierDelete: {
    call: (payload, clientRequestId) => suppliersApi.deleteSupplier((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['suppliers']]),
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
  },
  expenseUpdate: {
    call: (payload, clientRequestId) =>
      expensesApi.updateExpense({ ...(payload as UpdateExpensePayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['expenses']]),
  },
  expenseDelete: {
    call: (payload, clientRequestId) => expensesApi.deleteExpense((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['expenses']]),
  },
  expenseCategory: {
    call: (payload, clientRequestId) =>
      expensesApi.createExpenseCategory({
        ...(payload as { name: string; description?: string | null }),
        clientRequestId,
      }),
    invalidate: (qc) => invalidateKeys(qc, [['expense-categories']]),
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
  },
  branchUpdate: {
    call: (payload, clientRequestId) =>
      branchesApi.updateBranch({ ...(payload as UpdateBranchPayload), clientRequestId }),
    invalidate: (qc) => invalidateKeys(qc, [['branches']]),
  },
  branchDelete: {
    call: (payload, clientRequestId) => branchesApi.deleteBranch((payload as { id: string }).id, clientRequestId),
    invalidate: (qc) => invalidateKeys(qc, [['branches']]),
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
