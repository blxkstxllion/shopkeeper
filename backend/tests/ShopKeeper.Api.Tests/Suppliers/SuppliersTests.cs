namespace ShopKeeper.Api.Tests.Suppliers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Suppliers.Commands;
using ShopKeeper.Application.Suppliers.Queries;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Infrastructure.Identity;

public class SuppliersTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task CreateSupplier_ThenList_ReturnsIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", "Kofi Mensah", "0244000000", "kofi@accradist.test", "Accra"),
            CancellationToken.None);

        Assert.True(supplier.IsActive);

        var all = await new GetSuppliersQueryHandler(context).Handle(new GetSuppliersQuery(), CancellationToken.None);
        Assert.Single(all);
        Assert.Equal("Accra Distributors", all[0].Name);
    }

    [Fact]
    public async Task UpdateSupplier_ChangesFields()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);

        await new UpdateSupplierCommandHandler(context, owner).Handle(
            new UpdateSupplierCommand(supplier.Id, "Accra Distributors Ltd", "Ama Boateng", "0244111111", null, null, true),
            CancellationToken.None);

        var updated = await context.Suppliers.SingleAsync(s => s.Id == supplier.Id);
        Assert.Equal("Accra Distributors Ltd", updated.Name);
        Assert.Equal("Ama Boateng", updated.ContactName);
    }

    [Fact]
    public async Task DeleteSupplier_SoftDeletes_ProductKeepsSupplierLink()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);

        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        var storedSupplier = await context.Suppliers.SingleAsync(s => s.Id == supplier.Id);
        Assert.False(storedSupplier.IsActive);

        var storedProduct = await context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(supplier.Id, storedProduct.SupplierId);

        var active = await new GetSuppliersQueryHandler(context).Handle(new GetSuppliersQuery(), CancellationToken.None);
        Assert.Empty(active);
    }

    [Fact]
    public async Task RestockFromSupplier_IncreasesStock_AndTagsInventoryTransaction()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var mediator = new TestSender(context, owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);

        var newQuantity = await new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplier.Id, product.Id, seeded.BranchId, 25, null), CancellationToken.None);

        Assert.Equal(75, newQuantity);

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(75, stock.QuantityOnHand);

        var transaction = await context.InventoryTransactions.SingleAsync(t => t.QuantityChange == 25);
        Assert.Equal("Supplier", transaction.ReferenceType);
        Assert.Equal(supplier.Id, transaction.ReferenceId);
    }

    [Fact]
    public async Task RestockFromSupplier_WithUnitCost_UpdatesProductCostPrice()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var mediator = new TestSender(context, owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);

        await new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplier.Id, product.Id, seeded.BranchId, 25, 3.50m), CancellationToken.None);

        var storedProduct = await context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(3.50m, storedProduct.CostPrice);
    }

    [Fact]
    public async Task GetSupplierRestockHistory_ReturnsOnlyThatSuppliersTransactions()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var mediator = new TestSender(context, owner);

        var supplierA = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        var supplierB = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Kumasi Traders", null, null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, null),
            CancellationToken.None);

        await new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplierA.Id, product.Id, seeded.BranchId, 10, null), CancellationToken.None);
        await new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplierB.Id, product.Id, seeded.BranchId, 5, null), CancellationToken.None);

        var history = await new GetSupplierRestockHistoryQueryHandler(context).Handle(
            new GetSupplierRestockHistoryQuery(supplierA.Id), CancellationToken.None);

        Assert.Single(history);
        Assert.Equal(10, history[0].Quantity);
        Assert.Equal(product.Id, history[0].ProductId);
    }

    [Fact]
    public async Task RestockFromSupplier_InactiveSupplier_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var mediator = new TestSender(context, owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);

        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplier.Id, product.Id, seeded.BranchId, 25, null), CancellationToken.None));

        var stock = await context.ProductStocks.SingleAsync(s => s.ProductId == product.Id);
        Assert.Equal(50, stock.QuantityOnHand); // unchanged - the blocked restock never applied
    }

    [Fact]
    public async Task CreateProduct_WithInactiveSupplier_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProduct_ChangingToInactiveSupplier_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, null),
            CancellationToken.None);
        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => new UpdateProductCommandHandler(context, owner).Handle(
            new UpdateProductCommand(
                product.Id, "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, true, supplier.Id),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProduct_KeepingExistingInactiveSupplierUnchanged_StillSucceeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);
        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        // Editing an unrelated field (price) on a product that already points at a
        // since-deactivated supplier must not be blocked - only a *change* of supplier is gated.
        await new UpdateProductCommandHandler(context, owner).Handle(
            new UpdateProductCommand(
                product.Id, "Coca-Cola 500ml", "SKU-001", null, null, null, null, 6.00m, 3.00m, 10, 20, true, true, supplier.Id),
            CancellationToken.None);

        var updated = await context.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(6.00m, updated.SellingPrice);
        Assert.Equal(supplier.Id, updated.SupplierId);
    }

    [Fact]
    public async Task GetSupplierRestockHistory_ForNowInactiveSupplier_StillReturnsHistory()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var mediator = new TestSender(context, owner);

        var supplier = await new CreateSupplierCommandHandler(context, owner).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None);
        var product = await new CreateProductCommandHandler(context, owner).Handle(
            new CreateProductCommand(
                "Coca-Cola 500ml", "SKU-001", null, null, null, null, 5.00m, 3.00m, 10, 20, true, 50, seeded.BranchId, supplier.Id),
            CancellationToken.None);
        await new RestockFromSupplierCommandHandler(context, owner, mediator).Handle(
            new RestockFromSupplierCommand(supplier.Id, product.Id, seeded.BranchId, 10, null), CancellationToken.None);

        await new DeleteSupplierCommandHandler(context, owner).Handle(new DeleteSupplierCommand(supplier.Id), CancellationToken.None);

        var history = await new GetSupplierRestockHistoryQueryHandler(context).Handle(
            new GetSupplierRestockHistoryQuery(supplier.Id), CancellationToken.None);

        Assert.Single(history); // past restock history stays intact and queryable
        Assert.Equal(10, history[0].Quantity);
    }

    [Fact]
    public async Task Cashier_CannotManageSuppliers()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.Cashier].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateSupplierCommandHandler(context, cashier).Handle(
            new CreateSupplierCommand("Accra Distributors", null, null, null, null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
