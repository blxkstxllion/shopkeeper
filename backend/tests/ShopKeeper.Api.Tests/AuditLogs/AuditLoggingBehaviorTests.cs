namespace ShopKeeper.Api.Tests.AuditLogs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;
using MediatR;

/// <summary>
/// Unlike every other test in this suite, these go through a real MediatR pipeline (the rest
/// construct command handlers directly, bypassing IPipelineBehaviors entirely) - this is the
/// only way to actually exercise AuditLoggingBehavior, which runs as a pipeline behavior rather
/// than being called from any handler.
/// </summary>
public class AuditLoggingBehaviorTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private ISender BuildSender(IAppDbContext context, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IAppDbContext>(context);
        services.AddSingleton(currentUser);
        services.AddSingleton<IPasswordHasher>(_hasher);
        services.AddSingleton<IJwtTokenService>(_jwt);
        services.AddSingleton<IEmailSender>(new TestEmailSender());
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Command_ThroughPipeline_WritesAuditLogWithRedactedPassword()
    {
        var setupUser = new TestCurrentUserService();
        var context = _db.CreateContext(setupUser);
        var sender = BuildSender(context, setupUser);

        await sender.Send(new RegisterCommand("audit@shop.test", "Passw0rd!", "Ama", "Owusu", "203.0.113.5"), CancellationToken.None);

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Register", log.Action);
        Assert.Null(log.BusinessId); // no active business yet at registration time
        Assert.NotNull(log.NewValue);
        Assert.DoesNotContain("Passw0rd!", log.NewValue);
        Assert.Contains("[REDACTED]", log.NewValue);
        Assert.Equal("127.0.0.1", log.IpAddress); // from ICurrentUserService, not the command's own IpAddress param
    }

    [Fact]
    public async Task VoidCommand_ThroughPipeline_IsStillAudited()
    {
        // Regression test for a real bug (see ValidationBehavior's comment): every pipeline
        // behavior silently never ran for a plain (void) IRequest command - every other test in
        // this file happens to use a typed IRequest{T} command, which is exactly why it went
        // uncaught. ChangePasswordCommand is void.
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId);
        await sender.Send(new ChangePasswordCommand(owner.UserId!.Value, "Passw0rd!", "NewPassw0rd!"), CancellationToken.None);

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Action == "ChangePassword");
        Assert.Equal(user.Id, log.UserId);
        Assert.NotNull(log.NewValue);
        Assert.DoesNotContain("NewPassw0rd!", log.NewValue);
        Assert.Contains("[REDACTED]", log.NewValue);
    }

    [Fact]
    public async Task Command_ThroughPipeline_DoesNotAuditQueries()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        await sender.Send(new ShopKeeper.Application.Employees.Queries.GetBusinessUsersQuery(), CancellationToken.None);

        Assert.Empty(context.AuditLogs.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Command_ThroughPipeline_SetsEntityTypeAndEntityIdFromCommandShape()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        var product = await sender.Send(
            new CreateProductCommand("Widget", "SKU-AUDIT", null, null, null, null, 10m, 6m, 0, true, 5, seeded.BranchId),
            CancellationToken.None);

        await sender.Send(
            new AdjustStockCommand(product.Id, seeded.BranchId, -2, "Damaged"), CancellationToken.None);

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Action == "AdjustStock");
        Assert.Equal("Stock", log.EntityType);
        Assert.Equal(product.Id, log.EntityId);
        Assert.Equal(seeded.BusinessId, log.BusinessId);
    }

    [Fact]
    public async Task Command_ThroughPipeline_StillSucceeds_IfAuditWriteFails()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var innerContext = _db.CreateContext(owner);
        var failingDb = new FailingAuditSaveDbContext(innerContext);
        var sender = BuildSender(failingDb, owner);

        // The audit behavior's own dedicated SaveChangesAsync call (adding only an AuditLog row)
        // throws every time here, but the command's own business save is unaffected - proving a
        // broken audit write can never surface as if the real operation failed.
        var product = await sender.Send(
            new CreateProductCommand("Gadget", "SKU-AUDIT-2", null, null, null, null, 10m, 6m, 0, true, 3, seeded.BranchId),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Gadget", product.Name);

        var stored = await innerContext.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal("Gadget", stored.Name);
        Assert.Empty(innerContext.AuditLogs.IgnoreQueryFilters()); // the audit row genuinely never landed
    }

    /// <summary>Delegates everything to a real AppDbContext except SaveChangesAsync, which fails
    /// whenever the only pending change is an AuditLog insert - i.e. specifically the audit
    /// behavior's own dedicated save, never a command's own business-logic save (which always
    /// includes at least one non-AuditLog entity).</summary>
    private sealed class FailingAuditSaveDbContext(AppDbContext inner) : IAppDbContext
    {
        public DbSet<User> Users => inner.Users;
        public DbSet<Business> Businesses => inner.Businesses;
        public DbSet<BusinessUser> BusinessUsers => inner.BusinessUsers;
        public DbSet<Role> Roles => inner.Roles;
        public DbSet<Permission> Permissions => inner.Permissions;
        public DbSet<RolePermission> RolePermissions => inner.RolePermissions;
        public DbSet<Branch> Branches => inner.Branches;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<AuditLog> AuditLogs => inner.AuditLogs;
        public DbSet<BusinessSetting> BusinessSettings => inner.BusinessSettings;
        public DbSet<ProductCategory> ProductCategories => inner.ProductCategories;
        public DbSet<Supplier> Suppliers => inner.Suppliers;
        public DbSet<Customer> Customers => inner.Customers;
        public DbSet<Product> Products => inner.Products;
        public DbSet<ProductStock> ProductStocks => inner.ProductStocks;
        public DbSet<InventoryTransaction> InventoryTransactions => inner.InventoryTransactions;
        public DbSet<Sale> Sales => inner.Sales;
        public DbSet<SaleItem> SaleItems => inner.SaleItems;
        public DbSet<Payment> Payments => inner.Payments;
        public DbSet<Refund> Refunds => inner.Refunds;
        public DbSet<RefundItem> RefundItems => inner.RefundItems;
        public DbSet<ExpenseCategory> ExpenseCategories => inner.ExpenseCategories;
        public DbSet<Expense> Expenses => inner.Expenses;
        public DbSet<PendingInvitation> PendingInvitations => inner.PendingInvitations;
        public DbSet<JoinRequest> JoinRequests => inner.JoinRequests;
        public DbSet<Notification> Notifications => inner.Notifications;
        public DbSet<NotificationPreference> NotificationPreferences => inner.NotificationPreferences;
        public DbSet<PaystackWebhookEvent> PaystackWebhookEvents => inner.PaystackWebhookEvents;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var added = inner.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList();
            if (added.Count > 0 && added.All(e => e.Entity is AuditLog))
            {
                throw new InvalidOperationException("Simulated audit-log write failure.");
            }

            return inner.SaveChangesAsync(cancellationToken);
        }
    }

    public void Dispose() => _db.Dispose();
}
