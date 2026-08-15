namespace ShopKeeper.Api.Tests.Tenancy;
using ShopKeeper.Application.Common.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Inventory.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// A Cashier or Branch Manager is pinned to one branch (BusinessUser.BranchId). Without
/// server-side enforcement, nothing stops them from passing a different branch's id in a
/// request body and acting on stock/sales they shouldn't have access to - see
/// CurrentUserServiceExtensions.RequireBranchAccess.
/// </summary>
public class BranchAccessTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(Guid BranchAId, Guid BranchBId, Guid ProductId, AppDbContext Context, TestCurrentUserService Cashier)> SeedTwoBranchesWithCashierAsync()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Second Branch", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Widget", "SKU-BR", null, null, null, null, 10m, 6m, 5, 10, true, 20, seeded.BranchId),
            CancellationToken.None);

        // Give the second branch its own stock too, so a successful cross-branch attempt would be detectable.
        context.ProductStocks.Add(new ProductStock { BusinessId = seeded.BusinessId, ProductId = product.Id, BranchId = branchB.Id, QuantityOnHand = 20 });
        await context.SaveChangesAsync(CancellationToken.None);

        var cashier = new TestCurrentUserService
        {
            UserId = seeded.OwnerId, // reusing the seeded user's id is fine - only BranchId/permissions matter here
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId, // pinned to the FIRST branch
            IsOwner = false,
            PermissionsList = [Domain.Constants.PermissionKeys.SalesCreate, Domain.Constants.PermissionKeys.InventoryModify],
        };

        return (seeded.BranchId, branchB.Id, product.Id, context, cashier);
    }

    [Fact]
    public async Task CreateSale_InOwnBranch_Succeeds()
    {
        var (branchAId, _, productId, context, cashier) = await SeedTwoBranchesWithCashierAsync();

        var sale = await new CreateSaleCommandHandler(context, cashier, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(branchAId, [new SaleLineInput(productId, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);

        Assert.Equal("Completed", sale.Status);
    }

    [Fact]
    public async Task CreateSale_InOtherBranch_ThrowsForbidden()
    {
        var (_, branchBId, productId, context, cashier) = await SeedTwoBranchesWithCashierAsync();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateSaleCommandHandler(context, cashier, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(branchBId, [new SaleLineInput(productId, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None));
    }

    [Fact]
    public async Task AdjustStock_InOtherBranch_ThrowsForbidden()
    {
        var (_, branchBId, productId, context, cashier) = await SeedTwoBranchesWithCashierAsync();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new AdjustStockCommandHandler(context, cashier, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(productId, branchBId, 5, "Should not be allowed"), CancellationToken.None));
    }

    [Fact]
    public async Task Owner_WithNoFixedBranch_CanActOnAnyBranch()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Second Branch", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand("Gadget", "SKU-OWN", null, null, null, null, 10m, 6m, 5, 10, true, 10, branchB.Id),
            CancellationToken.None);

        // Owner has BranchId == null, so acting on branchB (not the business's main branch) must not throw.
        var quantity = await new AdjustStockCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new AdjustStockCommand(product.Id, branchB.Id, 5, "Owner adjusting a non-default branch"), CancellationToken.None);

        Assert.Equal(15, quantity);
    }

    public void Dispose() => _db.Dispose();
}
