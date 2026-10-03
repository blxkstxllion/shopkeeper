namespace ShopKeeper.Domain.Enums;

public enum CustomerLedgerEntryType
{
    /// <summary>A credit sale's unpaid shortfall - increases what the customer owes.</summary>
    Charge,

    /// <summary>A repayment against the customer's balance - decreases what they owe.</summary>
    Payment,

    /// <summary>The portion of a refund applied against the customer's balance rather than
    /// paid out in cash/card - decreases what they owe. Never the full refund amount, only
    /// whatever the cashier explicitly chose to apply - see RefundSaleCommand.</summary>
    RefundCredit,
}
