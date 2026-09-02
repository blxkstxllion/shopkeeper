namespace ShopKeeper.Application.Common.Interfaces;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Application-layer view of the EF Core context. Infrastructure's AppDbContext
/// implements this so Application never takes a hard dependency on EF Core's
/// concrete DbContext type.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Business> Businesses { get; }
    DbSet<BusinessUser> BusinessUsers { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Branch> Branches { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<BusinessSetting> BusinessSettings { get; }

    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductStock> ProductStocks { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<RefundItem> RefundItems { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<PendingInvitation> PendingInvitations { get; }
    DbSet<JoinRequest> JoinRequests { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<PaystackWebhookEvent> PaystackWebhookEvents { get; }
    DbSet<ScheduledReport> ScheduledReports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
