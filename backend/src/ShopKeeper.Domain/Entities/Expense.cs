namespace ShopKeeper.Domain.Entities;

using ShopKeeper.Domain.Common;

/// <summary>
/// A one-time, dated expense entry. Recurring auto-generation, attachments, and an approval
/// workflow are all things the master spec calls out as future "architecture" - deliberately
/// not built here, since a toggle with no scheduler/approver behind it would just be a fake
/// control. This is real: every row is something someone actually recorded happening.
/// </summary>
public class Expense : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    /// <summary>Null = a business-wide expense (e.g. head-office rent) not tied to one branch.</summary>
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = default!;

    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = default!;

    /// <summary>Soft-delete flag - financial records are voided, never hard-deleted.</summary>
    public bool IsActive { get; set; } = true;
}
