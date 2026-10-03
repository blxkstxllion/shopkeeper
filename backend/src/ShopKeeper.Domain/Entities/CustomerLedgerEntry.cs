namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Enums;

/// <summary>
/// Append-only ledger row for every change to a customer's account balance - mirrors
/// InventoryTransaction exactly, same discipline: nothing here should ever be updated or
/// deleted, only added to. Customer.CurrentBalance is the fast-read materialization of this
/// ledger's running sum, maintained transactionally alongside every row here, the same
/// relationship ProductStock.QuantityOnHand has to InventoryTransaction.
/// </summary>
public class CustomerLedgerEntry : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public CustomerLedgerEntryType Type { get; set; }

    /// <summary>Positive for Charge (increases what's owed), negative for Payment and
    /// RefundCredit (decreases what's owed).</summary>
    public decimal Amount { get; set; }

    /// <summary>Snapshot of Customer.CurrentBalance immediately after this entry, for audit
    /// clarity - same reason InventoryTransaction.QuantityAfter exists.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>e.g. "Sale" + SaleId for a Charge, "Refund" + RefundId for a RefundCredit, or
    /// "CustomerPayment" + this same entry's own Id for a Payment (a repayment has no separate
    /// parent document - the ledger entry itself is the full record) - what caused this entry
    /// to exist.</summary>
    public string ReferenceType { get; set; } = default!;
    public Guid ReferenceId { get; set; }

    /// <summary>Only meaningful for Payment - how the customer actually repaid. Null for
    /// Charge/RefundCredit, which aren't a tender event themselves.</summary>
    public PaymentMethod? Method { get; set; }
    public string? ReferenceNumber { get; set; }

    public string? Note { get; set; }

    public Guid CreatedByUserId { get; set; }

    /// <summary>Idempotency key for RecordCustomerPaymentCommand, same dedicated
    /// precheck/partial-unique-index/catch-the-race mechanism as Sale.ClientRequestId and
    /// Refund.ClientRequestId - a repayment moves real money and a customer's balance, so a
    /// retried submission must never apply twice. Only ever set on a Payment-type entry; Charge
    /// and RefundCredit ride along on CreateSaleCommand's and RefundSaleCommand's own
    /// idempotency instead.</summary>
    public Guid? ClientRequestId { get; set; }
}
