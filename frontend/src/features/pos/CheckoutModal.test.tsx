import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CheckoutModal } from './CheckoutModal'
import * as offlineQueryModule from '@/offline/useOfflineQuery'
import * as salesApi from '@/api/sales'
import * as customersApi from '@/api/customers'
import * as businessSettingsApi from '@/api/businessSettings'
import * as mutationQueueModule from '@/offline/mutationQueue'
import * as AuthContextModule from '@/contexts/AuthContext'
import * as onlineStatusModule from '@/hooks/useOnlineStatus'
import { ApiError } from '@/lib/api-client'
import type { CartLine } from './cart'
import type { Sale } from '@/types/sale'
import type { BusinessSettings } from '@/types/businessSettings'

vi.mock('@/offline/useOfflineQuery')
vi.mock('@/api/sales')
vi.mock('@/api/customers')
vi.mock('@/api/businessSettings')
vi.mock('@/offline/mutationQueue')
vi.mock('@/offline/customerCache')
vi.mock('@/hooks/useOnlineStatus')
vi.mock('@/contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})

const noTax: BusinessSettings = {
  businessId: 'b1',
  name: 'Ama Shop',
  legalName: null,
  businessType: 'Retail',
  businessTypeOther: null,
  country: 'Ghana',
  currencyCode: 'GHS',
  timeZone: 'Africa/Accra',
  logoUrl: null,
  colorTheme: 'green',
  taxEnabled: false,
  taxIdNumber: null,
  taxRatePercent: 0,
  taxInclusivePricing: false,
}

const line: CartLine = {
  product: {
    productId: 'p1',
    name: 'Widget',
    sku: 'W1',
    barcode: null,
    imageUrl: null,
    categoryId: null,
    sellingPrice: 10,
    trackInventory: true,
    quantityOnHand: 50,
  },
  quantity: 2,
  discountAmount: 0,
}

const sale: Sale = {
  id: 's1',
  saleNumber: 'SALE-001',
  branchId: 'b1',
  branchName: 'Main branch',
  customerId: null,
  customerName: null,
  cashierUserId: 'u1',
  cashierName: 'Kofi',
  subtotal: 20,
  discountAmount: 0,
  taxAmount: 0,
  total: 20,
  totalCost: 10,
  grossProfit: 10,
  status: 'Completed',
  voidedAt: null,
  voidReason: null,
  createdAt: '2026-10-01T00:00:00Z',
  items: [],
  payments: [],
}

const onSuccess = vi.fn()
const onClose = vi.fn()

function renderModal(overrides: Partial<Parameters<typeof CheckoutModal>[0]> = {}) {
  const queryClient = new QueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <CheckoutModal
        isOpen
        onClose={onClose}
        lines={[line]}
        discountAmount={0}
        branchId="b1"
        branchName="Main branch"
        onSuccess={onSuccess}
        {...overrides}
      />
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(offlineQueryModule.useOfflineSingletonQuery).mockReturnValue({ data: noTax } as never)
  vi.mocked(customersApi.getCustomers).mockResolvedValue({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 200,
    totalPages: 0,
  })
  vi.mocked(businessSettingsApi.getBusinessSettings).mockResolvedValue(noTax)
  vi.mocked(onlineStatusModule.useOnlineStatus).mockReturnValue(true)
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    activeBusiness: { businessId: 'b1' },
  } as never)
  vi.mocked(salesApi.createSale).mockResolvedValue(sale)
  vi.mocked(mutationQueueModule.enqueueMutation).mockResolvedValue(undefined)
})

describe('CheckoutModal', () => {
  it('disables Confirm payment until the full total is covered', async () => {
    renderModal()

    expect(screen.getByRole('button', { name: /confirm payment/i })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))

    expect(screen.getByRole('button', { name: /confirm payment/i })).toBeEnabled()
  })

  it('submits the sale with the clicked payment method and amount, and reports success', async () => {
    renderModal()

    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    await waitFor(() =>
      expect(salesApi.createSale).toHaveBeenCalledWith(
        expect.objectContaining({
          branchId: 'b1',
          items: [{ productId: 'p1', quantity: 2, discountAmount: 0 }],
          discountAmount: 0,
          payments: [{ method: 'Cash', amount: 20, referenceNumber: null }],
          customerId: null,
          allowCredit: false,
          clientRequestId: expect.any(String),
        }),
      ),
    )
    expect(onSuccess).toHaveBeenCalledWith(sale)
  })

  it('supports splitting the total across two payment methods', async () => {
    renderModal()

    // Adding a second method only has an effect while something is still owed - Cash
    // defaults to the full total, so it must be reduced before Card becomes clickable
    // (the method buttons disable once remaining <= 0).
    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))
    const cashAmount = screen.getAllByRole('spinbutton')[0]
    await userEvent.clear(cashAmount)
    await userEvent.type(cashAmount, '15')

    await userEvent.click(screen.getByRole('button', { name: /^card$/i }))

    expect(screen.getByRole('button', { name: /confirm payment/i })).toBeEnabled()

    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    await waitFor(() =>
      expect(salesApi.createSale).toHaveBeenCalledWith(
        expect.objectContaining({
          payments: [
            { method: 'Cash', amount: 15, referenceNumber: null },
            { method: 'Card', amount: 5, referenceNumber: null },
          ],
        }),
      ),
    )
  })

  it('surfaces a real rejection (e.g. insufficient stock) as an error and never queues it offline', async () => {
    vi.mocked(salesApi.createSale).mockRejectedValue(
      new ApiError({ title: 'Not enough stock for Widget.', status: 409, errors: null }),
    )
    renderModal()

    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    expect(await screen.findByText('Not enough stock for Widget.')).toBeInTheDocument()
    expect(mutationQueueModule.enqueueMutation).not.toHaveBeenCalled()
    expect(onSuccess).not.toHaveBeenCalled()
  })

  it('queues the sale for later sync when offline, without ever calling createSale', async () => {
    vi.mocked(onlineStatusModule.useOnlineStatus).mockReturnValue(false)
    renderModal()

    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    await waitFor(() => expect(mutationQueueModule.enqueueMutation).toHaveBeenCalled())
    expect(salesApi.createSale).not.toHaveBeenCalled()

    const queued = vi.mocked(mutationQueueModule.enqueueMutation).mock.calls[0][1]
    expect(queued.entityType).toBe('sale')
    expect(queued.payload).toMatchObject({ branchId: 'b1' })
    expect(onSuccess).toHaveBeenCalledWith(expect.objectContaining({ queued: true, branchName: 'Main branch' }))
  })

  it('falls back to queuing when createSale fails at the network layer despite isOnline being true', async () => {
    vi.mocked(salesApi.createSale).mockRejectedValue(new ApiError({ title: 'Network Error', status: 0, errors: null }))
    renderModal()

    await userEvent.click(screen.getByRole('button', { name: /^cash$/i }))
    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    await waitFor(() => expect(mutationQueueModule.enqueueMutation).toHaveBeenCalled())
    expect(onSuccess).toHaveBeenCalledWith(expect.objectContaining({ queued: true }))
  })

  it('allows confirming with an unpaid remainder when selling on credit to a chosen customer', async () => {
    vi.mocked(customersApi.getCustomers).mockResolvedValue({
      items: [
        {
          id: 'c1',
          name: 'Ama',
          phone: null,
          email: null,
          address: null,
          isActive: true,
          currentBalance: 0,
          balanceRowVersion: 1,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 200,
      totalPages: 1,
    })
    renderModal()

    await screen.findByRole('option', { name: 'Ama' })
    await userEvent.selectOptions(screen.getByLabelText('Customer (optional)'), 'c1')
    await userEvent.click(screen.getByRole('checkbox', { name: /sell on credit/i }))

    // No payment method clicked at all - the full 20 is owed to the customer's account.
    expect(screen.getByRole('button', { name: /confirm payment/i })).toBeEnabled()

    await userEvent.click(screen.getByRole('button', { name: /confirm payment/i }))

    await waitFor(() =>
      expect(salesApi.createSale).toHaveBeenCalledWith(
        expect.objectContaining({ customerId: 'c1', allowCredit: true, payments: [] }),
      ),
    )
  })
})
