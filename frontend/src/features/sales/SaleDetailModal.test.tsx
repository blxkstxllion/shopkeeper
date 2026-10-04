import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SaleDetailModal } from './SaleDetailModal'
import * as offlineQueryModule from '@/offline/useOfflineQuery'
import * as offlineMutationModule from '@/offline/useOfflineMutation'
import * as customersApi from '@/api/customers'
import * as onlineStatusModule from '@/hooks/useOnlineStatus'
import type { Sale } from '@/types/sale'
import type { CustomerDetail } from '@/types/customer'

vi.mock('@/offline/useOfflineQuery')
vi.mock('@/offline/useOfflineMutation')
vi.mock('@/api/customers')
vi.mock('@/hooks/useOnlineStatus')

const sale: Sale = {
  id: 's1',
  saleNumber: 'SALE-001',
  branchId: 'b1',
  branchName: 'Main branch',
  customerId: 'c1',
  customerName: 'Ama',
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
  items: [
    {
      id: 'i1',
      productId: 'p1',
      productName: 'Widget',
      sku: 'W1',
      quantity: 2,
      unitPrice: 10,
      unitCost: 5,
      discountAmount: 0,
      lineRevenue: 20,
      lineCost: 10,
      lineProfit: 10,
      refundedQuantity: 0,
      netAmountPaid: 20,
    },
  ],
  payments: [{ id: 'pay1', method: 'Cash', amount: 20, referenceNumber: null }],
}

const customerDetail: CustomerDetail = {
  id: 'c1',
  name: 'Ama',
  phone: null,
  email: null,
  address: null,
  isActive: true,
  currentBalance: 15,
  balanceRowVersion: 4,
  totalSpend: 100,
  averageSale: 50,
  purchaseCount: 2,
  lastPurchaseAt: null,
}

const refundMutateAsync = vi.fn().mockResolvedValue({ data: {}, queued: false, clientRequestId: 'x' })

function renderModal() {
  const queryClient = new QueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <SaleDetailModal saleId="s1" onClose={() => {}} />
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  refundMutateAsync.mockResolvedValue({ data: {}, queued: false, clientRequestId: 'x' })
  vi.mocked(offlineQueryModule.useOfflineSingletonQuery).mockReturnValue({ data: sale } as never)
  vi.mocked(offlineMutationModule.useOfflineMutation).mockReturnValue({
    mutateAsync: refundMutateAsync,
    isPending: false,
  } as never)
  vi.mocked(onlineStatusModule.useOnlineStatus).mockReturnValue(true)
  vi.mocked(customersApi.getCustomer).mockResolvedValue(customerDetail)
})

async function enterRefundModeWithOneUnit() {
  await userEvent.click(screen.getByRole('button', { name: /refund/i }))
  const qtyInput = screen.getByLabelText('Refund qty:')
  await userEvent.clear(qtyInput)
  await userEvent.type(qtyInput, '1')
}

describe('SaleDetailModal refund balance split', () => {
  it('shows the apply-to-balance control once the customer balance loads, defaulting to no split', async () => {
    renderModal()
    await enterRefundModeWithOneUnit()

    await waitFor(() => expect(customersApi.getCustomer).toHaveBeenCalledWith('c1'))
    expect(await screen.findByLabelText(/Apply to balance/)).toHaveValue(0)
    // Full preview (10) is paid out by default - the split is never inferred.
    expect(screen.getByText('Paid out')).toBeInTheDocument()
  })

  it('moves the paid-out amount down as the cashier applies more to the balance, clamped to the smaller of balance and preview', async () => {
    renderModal()
    await enterRefundModeWithOneUnit()

    const applyInput = await screen.findByLabelText(/Apply to balance/)
    await userEvent.clear(applyInput)
    await userEvent.type(applyInput, '999')

    // previewTotal for a 1-unit refund of a 2-unit/20-total line is 10, below the 15 balance -
    // so 10 is the clamp, not 15.
    await waitFor(() => expect(applyInput).toHaveValue(10))
  })

  it('submits applyToBalance and the customer balanceRowVersion when the cashier chooses a split', async () => {
    renderModal()
    await enterRefundModeWithOneUnit()

    const applyInput = await screen.findByLabelText(/Apply to balance/)
    await userEvent.clear(applyInput)
    await userEvent.type(applyInput, '4')

    await userEvent.type(screen.getByPlaceholderText('Reason (required)'), 'Customer returned item')
    await userEvent.click(screen.getByRole('button', { name: /confirm refund/i }))

    await waitFor(() =>
      expect(refundMutateAsync).toHaveBeenCalledWith({
        payload: expect.objectContaining({
          saleId: 's1',
          applyToBalance: 4,
          customerBalanceRowVersion: 4,
        }),
      }),
    )
  })

  it('omits the balance fields entirely when the cashier leaves the split at zero', async () => {
    renderModal()
    await enterRefundModeWithOneUnit()
    await screen.findByLabelText(/Apply to balance/)

    await userEvent.type(screen.getByPlaceholderText('Reason (required)'), 'Customer returned item')
    await userEvent.click(screen.getByRole('button', { name: /confirm refund/i }))

    await waitFor(() => expect(refundMutateAsync).toHaveBeenCalled())
    const payload = refundMutateAsync.mock.calls[0][0].payload
    expect(payload.applyToBalance).toBeUndefined()
    expect(payload.customerBalanceRowVersion).toBeUndefined()
  })

  it('falls back to a full payout with no split control when offline', async () => {
    vi.mocked(onlineStatusModule.useOnlineStatus).mockReturnValue(false)
    renderModal()
    await enterRefundModeWithOneUnit()

    expect(customersApi.getCustomer).not.toHaveBeenCalled()
    expect(screen.queryByLabelText(/Apply to balance/)).not.toBeInTheDocument()
    expect(screen.getByText(/requires a connection/)).toBeInTheDocument()
  })
})
