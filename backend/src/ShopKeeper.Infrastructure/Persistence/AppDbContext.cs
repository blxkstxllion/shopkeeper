namespace ShopKeeper.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Common;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Tenant isolation is enforced here, once, via global query filters keyed off
/// ICurrentUserService.BusinessId - every ITenantEntity DbSet is filtered
/// automatically on every query, so individual repositories/handlers cannot
/// accidentally leak data across businesses by forgetting a Where clause.
/// Use IgnoreQueryFilters() only in explicitly cross-tenant, trusted code paths
/// (e.g. TokenIssuer resolving a user's own memberships across businesses).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();

    /// <summary>
    /// Referenced as `this.TenantBusinessId` (implicitly, from inside an instance method) in the
    /// query filter lambdas below. EF Core caches the compiled model once per context type/process,
    /// so a filter that closed over the *injected ICurrentUserService parameter* directly would bake
    /// in whichever request's service instance happened to build the model first - a cross-tenant
    /// leak for every request after that. Referencing an instance member through `this` is EF Core's
    /// documented escape hatch: it specially rebinds `this` to whichever context instance is actually
    /// executing the query, so each request's own ICurrentUserService is consulted correctly.
    /// See https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy for the pattern.
    /// </summary>
    public Guid? TenantBusinessId => currentUser.BusinessId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(BuildTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                var filter = method.Invoke(this, null);
                entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter!);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private System.Linq.Expressions.LambdaExpression BuildTenantFilter<TEntity>()
        where TEntity : class, ITenantEntity
    {
        System.Linq.Expressions.Expression<Func<TEntity, bool>> filter =
            e => TenantBusinessId != null && e.BusinessId == TenantBusinessId;
        return filter;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
