namespace ShopKeeper.Api.Tests.Customers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Customers.Commands;
using ShopKeeper.Application.Customers.Queries;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class CustomersTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task CreateCustomer_ThenList_ReturnsIt()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", "0201234567", "kwame@example.test", "Osu, Accra"),
            CancellationToken.None);

        Assert.True(customer.IsActive);

        var all = await new GetCustomersQueryHandler(context, owner).Handle(
            new GetCustomersQuery(null, true, 1, 20), CancellationToken.None);
        Assert.Single(all.Items);
        Assert.Equal("Kwame Boateng", all.Items[0].Name);
    }

    [Fact]
    public async Task GetCustomers_SearchByPhone_FiltersResults()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", "0201234567", null, null), CancellationToken.None);
        await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Ama Serwaa", "0209999999", null, null), CancellationToken.None);

        var results = await new GetCustomersQueryHandler(context, owner).Handle(
            new GetCustomersQuery("0201234567", true, 1, 20), CancellationToken.None);

        Assert.Single(results.Items);
        Assert.Equal("Kwame Boateng", results.Items[0].Name);
    }

    [Fact]
    public async Task UpdateCustomer_ChangesFields()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None);

        await new UpdateCustomerCommandHandler(context, owner).Handle(
            new UpdateCustomerCommand(customer.Id, "Kwame B. Boateng", "0201234567", null, null, true),
            CancellationToken.None);

        var updated = await context.Customers.SingleAsync(c => c.Id == customer.Id);
        Assert.Equal("Kwame B. Boateng", updated.Name);
        Assert.Equal("0201234567", updated.Phone);
    }

    [Fact]
    public async Task DeleteCustomer_SoftDeletes_SaleHistoryUnaffected()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-CUST", null, null, null, null, 10m, 6m, 10, true, 20, seeded.BranchId),
            CancellationToken.None);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(product.Id, 2, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)],
                customer.Id),
            CancellationToken.None);

        await new DeleteCustomerCommandHandler(context, owner).Handle(new DeleteCustomerCommand(customer.Id), CancellationToken.None);

        var storedCustomer = await context.Customers.SingleAsync(c => c.Id == customer.Id);
        Assert.False(storedCustomer.IsActive);

        var storedSale = await context.Sales.SingleAsync(s => s.Id == sale.Id);
        Assert.Equal(customer.Id, storedSale.CustomerId);
    }

    [Fact]
    public async Task CreateSale_WithCustomer_LinksCustomerAndReturnsName()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-CUST2", null, null, null, null, 10m, 6m, 10, true, 20, seeded.BranchId),
            CancellationToken.None);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(product.Id, 2, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)],
                customer.Id),
            CancellationToken.None);

        Assert.Equal(customer.Id, sale.CustomerId);
        Assert.Equal("Kwame Boateng", sale.CustomerName);
    }

    [Fact]
    public async Task CreateSale_WithoutCustomer_StillWorks()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-WALKIN", null, null, null, null, 10m, 6m, 10, true, 20, seeded.BranchId),
            CancellationToken.None);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId,
                [new SaleLineInput(product.Id, 2, 0)],
                0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);

        Assert.Null(sale.CustomerId);
        Assert.Null(sale.CustomerName);
    }

    [Fact]
    public async Task GetCustomerDetail_ReturnsRealAggregatesOverSaleHistory()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-AGG", null, null, null, null, 10m, 6m, 10, true, 20, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], customer.Id),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 3, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 30m, null)], customer.Id),
            CancellationToken.None);

        var detail = await new GetCustomerDetailQueryHandler(context, owner).Handle(
            new GetCustomerDetailQuery(customer.Id), CancellationToken.None);

        Assert.Equal(50m, detail.TotalSpend);
        Assert.Equal(2, detail.PurchaseCount);
        Assert.Equal(25m, detail.AverageSale);
        Assert.NotNull(detail.LastPurchaseAt);
    }

    [Fact]
    public async Task Cashier_CanManageCustomers()
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

        var customer = await new CreateCustomerCommandHandler(context, cashier).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None);

        Assert.True(customer.IsActive);
    }

    [Fact]
    public async Task InventoryManager_CannotManageCustomers()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var inventoryManager = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = DefaultRoles.RolePermissionKeys[DefaultRoles.InventoryManager].ToList(),
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new CreateCustomerCommandHandler(context, inventoryManager).Handle(
            new CreateCustomerCommand("Kwame Boateng", null, null, null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
