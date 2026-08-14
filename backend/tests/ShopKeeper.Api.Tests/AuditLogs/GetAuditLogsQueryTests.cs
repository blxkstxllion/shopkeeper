namespace ShopKeeper.Api.Tests.AuditLogs;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.AuditLogs.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;

public class GetAuditLogsQueryTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task GetAuditLogs_ReturnsOnlyCurrentBusinessesRows()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerA@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "ownerB@shop.test");
        var ownerA = businessA.AsOwner();
        var context = _db.CreateContext(ownerA);

        context.AuditLogs.Add(new AuditLog { BusinessId = businessA.BusinessId, Action = "CreateProduct" });
        context.AuditLogs.Add(new AuditLog { BusinessId = businessB.BusinessId, Action = "CreateProduct" });
        context.AuditLogs.Add(new AuditLog { BusinessId = null, Action = "Register" }); // platform-level, no business yet
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetAuditLogsQueryHandler(context, ownerA).Handle(
            new GetAuditLogsQuery(null, null, null, null, null, 1, 50), CancellationToken.None);

        Assert.Single(result.Items); // Business B's row and the null-business row are both excluded
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetAuditLogs_FiltersByEntityTypeAndAction()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        context.AuditLogs.Add(new AuditLog { BusinessId = seeded.BusinessId, Action = "CreateProduct", EntityType = "Product" });
        context.AuditLogs.Add(new AuditLog { BusinessId = seeded.BusinessId, Action = "AdjustStock", EntityType = "Stock" });
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetAuditLogsQueryHandler(context, owner).Handle(
            new GetAuditLogsQuery("Stock", null, null, null, null, 1, 50), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("AdjustStock", result.Items[0].Action);
    }

    [Fact]
    public async Task GetAuditLogs_IncludesActorNameFromUser()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        context.AuditLogs.Add(new AuditLog { BusinessId = seeded.BusinessId, UserId = seeded.OwnerId, Action = "CreateProduct" });
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetAuditLogsQueryHandler(context, owner).Handle(
            new GetAuditLogsQuery(null, null, null, null, null, 1, 50), CancellationToken.None);

        Assert.Equal("Ama Owusu", result.Items[0].ActorName);
    }

    [Fact]
    public async Task GetAuditLogs_WithoutAuditLogsViewPermission_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Cashier].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new GetAuditLogsQueryHandler(context, cashier).Handle(
            new GetAuditLogsQuery(null, null, null, null, null, 1, 50), CancellationToken.None));
    }

    [Fact]
    public async Task GetAuditLogs_Paginates()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        for (var i = 0; i < 5; i++)
        {
            context.AuditLogs.Add(new AuditLog { BusinessId = seeded.BusinessId, Action = $"Action{i}" });
        }

        await context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetAuditLogsQueryHandler(context, owner).Handle(
            new GetAuditLogsQuery(null, null, null, null, null, 1, 2), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    public void Dispose() => _db.Dispose();
}
