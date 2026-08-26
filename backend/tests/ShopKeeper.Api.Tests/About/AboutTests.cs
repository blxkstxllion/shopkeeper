namespace ShopKeeper.Api.Tests.About;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.About.Commands;
using ShopKeeper.Application.About.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class AboutTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    /// <summary>Same backdating helper as ReportsTests - SaveChangesAsync only auto-stamps
    /// CreatedAt on Added (not Modified) entities, so a second save leaves it alone.</summary>
    private static async Task BackdateSaleAsync(AppDbContext context, Guid saleId, DateTimeOffset newCreatedAt)
    {
        var sale = await context.Sales.SingleAsync(s => s.Id == saleId);
        sale.CreatedAt = newCreatedAt;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UpdateBusinessAbout_PersistsDescriptionAndOwnerBio()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateBusinessAboutCommandHandler(context, owner).Handle(
            new UpdateBusinessAboutCommand("We sell fresh produce.", "Ama has run this shop for 10 years."), CancellationToken.None);

        var about = await new GetBusinessAboutQueryHandler(context, owner).Handle(new GetBusinessAboutQuery(), CancellationToken.None);
        Assert.Equal("We sell fresh produce.", about.Description);
        Assert.Equal("Ama has run this shop for 10 years.", about.OwnerBio);
    }

    [Fact]
    public async Task UpdateBusinessAbout_UserWithoutSettingsManagePermission_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = [],
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new UpdateBusinessAboutCommandHandler(context, cashier).Handle(
            new UpdateBusinessAboutCommand("Hacked description", null), CancellationToken.None));
    }

    [Fact]
    public async Task GetBusinessAbout_NoSalesYet_ReturnsEmptySalesByYear()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var about = await new GetBusinessAboutQueryHandler(context, owner).Handle(new GetBusinessAboutQuery(), CancellationToken.None);

        Assert.Empty(about.SalesByYear);
    }

    [Fact]
    public async Task GetBusinessAbout_ComputesBestAndWorstYearFromSalesAcrossYears()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ABOUT", null, null, null, null, 10m, 6m, 0, 0, true, 100, seeded.BranchId),
            CancellationToken.None);

        var strongYearSale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 10, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 100m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, strongYearSale.Id, new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var weakYearSale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        await BackdateSaleAsync(context, weakYearSale.Id, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var about = await new GetBusinessAboutQueryHandler(context, owner).Handle(new GetBusinessAboutQuery(), CancellationToken.None);

        Assert.Equal(2, about.SalesByYear.Count);
        var year2025 = about.SalesByYear.Single(y => y.Year == 2025);
        var year2026 = about.SalesByYear.Single(y => y.Year == 2026);
        Assert.Equal(100m, year2025.Revenue);
        Assert.Equal(10m, year2026.Revenue);
        Assert.True(year2025.Revenue > year2026.Revenue);
    }

    [Fact]
    public async Task GetBusinessAbout_UserWithoutReportsViewPermission_SeesDescriptionButNotSalesByYear()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var ownerContext = _db.CreateContext(owner);

        await new UpdateBusinessAboutCommandHandler(ownerContext, owner).Handle(
            new UpdateBusinessAboutCommand("Visible to everyone.", null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(ownerContext, owner, new PlanLimitService(ownerContext)).Handle(
            new CreateProductCommand("Widget", "SKU-NOPERM", null, null, null, null, 10m, 6m, 0, 0, true, 100, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(ownerContext, owner, new NotificationDispatcher(ownerContext)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var restrictedUser = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = [],
        };
        var restrictedContext = _db.CreateContext(restrictedUser);

        var about = await new GetBusinessAboutQueryHandler(restrictedContext, restrictedUser).Handle(new GetBusinessAboutQuery(), CancellationToken.None);

        Assert.Equal("Visible to everyone.", about.Description);
        Assert.Empty(about.SalesByYear);
    }

    public void Dispose() => _db.Dispose();
}
