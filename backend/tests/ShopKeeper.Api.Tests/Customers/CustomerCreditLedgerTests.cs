namespace ShopKeeper.Api.Tests.Customers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Customers.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class CustomerCreditLedgerTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private async Task<(PosTestFixture.SeededBusiness Seeded, AppDbContext Context, TestCurrentUserService Owner, Guid ProductId, Guid CustomerId)>
        SeedWithProductAndCustomerAsync(decimal sellingPrice = 10m, decimal costPrice = 6m, int initialQuantity = 20)
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-CREDIT", null, null, null, null, sellingPrice, costPrice, 0, true, initialQuantity, seeded.BranchId),
            CancellationToken.None);

        var customer = await new CreateCustomerCommandHandler(context, owner).Handle(
            new CreateCustomerCommand("Ama Mensah", null, null, null), CancellationToken.None);

        return (seeded, context, owner, product.Id, customer.Id);
    }

    [Fact]
    public async Task CreateSale_WithAllowCreditAndShortfall_ChargesCustomerAndUpdatesBalance()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], // 30 short of the 50 total
                CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);

        Assert.Equal(50m, sale.Total);

        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(30m, customer.CurrentBalance);

        var entry = await context.CustomerLedgerEntries.AsNoTracking().SingleAsync(e => e.CustomerId == customerId);
        Assert.Equal(CustomerLedgerEntryType.Charge, entry.Type);
        Assert.Equal(30m, entry.Amount);
        Assert.Equal(30m, entry.BalanceAfter);
        Assert.Equal("Sale", entry.ReferenceType);
        Assert.Equal(sale.Id, entry.ReferenceId);
    }

    [Fact]
    public async Task CreateSale_ShortfallWithoutAllowCredit_StillRejected()
    {
        // Unchanged from before credit sales existed - AllowCredit must be explicitly set.
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: false),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateSale_ShortfallWithAllowCreditButNoCustomer_Rejected()
    {
        // No debt on an anonymous walk-in, even with AllowCredit set.
        var (seeded, context, owner, productId, _) = await SeedWithProductAndCustomerAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: null, AllowCredit: true),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateSale_Overpayment_RejectedEvenWithAllowCredit()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        await Assert.ThrowsAsync<ConflictException>(() => new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 100m, null)], CustomerId: customerId, AllowCredit: true),
            CancellationToken.None));
    }

    [Fact]
    public async Task RecordCustomerPayment_ReducesBalanceAndRecordsEntry()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);

        var result = await new RecordCustomerPaymentCommandHandler(context, owner).Handle(
            new RecordCustomerPaymentCommand(customerId, 10m, PaymentMethod.Cash, null, "Partial repayment"), CancellationToken.None);

        Assert.Equal(-10m, result.Amount);
        Assert.Equal(20m, result.BalanceAfter);

        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(20m, customer.CurrentBalance);
    }

    [Fact]
    public async Task RecordCustomerPayment_ReplayedWithSameClientRequestId_DoesNotDoubleApply()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);

        var clientRequestId = Guid.NewGuid();
        var first = await new RecordCustomerPaymentCommandHandler(context, owner).Handle(
            new RecordCustomerPaymentCommand(customerId, 10m, PaymentMethod.Cash, null, null, clientRequestId), CancellationToken.None);
        var replay = await new RecordCustomerPaymentCommandHandler(context, owner).Handle(
            new RecordCustomerPaymentCommand(customerId, 10m, PaymentMethod.Cash, null, null, clientRequestId), CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(20m, customer.CurrentBalance); // only reduced once, not twice
    }

    [Fact]
    public async Task RecordCustomerPayment_GenuineConcurrentReplay_ExactlyOneEntryCreated()
    {
        using var db = new ConcurrentSqliteTestDatabase();
        var hasher = new BcryptPasswordHasher();
        var jwt = new JwtTokenService(Options.Create(PosTestFixture.JwtTestSettings));
        var seeded = await PosTestFixture.SeedAsync(db, hasher, jwt);
        var owner = seeded.AsOwner();
        var setupContext = db.CreateContext(owner);

        var customer = await new CreateCustomerCommandHandler(setupContext, owner).Handle(
            new CreateCustomerCommand("Ama Mensah", null, null, null), CancellationToken.None);

        var clientRequestId = Guid.NewGuid();

        Task<ShopKeeper.Application.Customers.Dtos.CustomerLedgerEntryDto> Send()
        {
            var context = db.CreateContext(owner);
            return new RecordCustomerPaymentCommandHandler(context, owner).Handle(
                new RecordCustomerPaymentCommand(customer.Id, 10m, PaymentMethod.Cash, null, null, clientRequestId), CancellationToken.None);
        }

        var results = await Task.WhenAll(Send(), Send());

        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(await setupContext.CustomerLedgerEntries.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RefundSale_WithApplyToBalance_ReducesBalanceAndSplitsCorrectly()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync(sellingPrice: 10m, initialQuantity: 20);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: true), // $30 charged
            CancellationToken.None);
        var saleItemId = sale.Items.Single().Id;

        var customerBeforeRefund = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(30m, customerBeforeRefund.CurrentBalance);

        // Refund 2 units ($20 of the $50 sale). Apply $15 to the balance, the rest pays out.
        var refund = await new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(
                sale.Id, [new RefundLineInput(saleItemId, 2)], "Customer returned 2 units",
                ApplyToBalance: 15m, CustomerBalanceRowVersion: customerBeforeRefund.RowVersion),
            CancellationToken.None);

        Assert.Equal(20m, refund.TotalAmount);
        Assert.Equal(15m, refund.AmountAppliedToBalance);

        var customerAfterRefund = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(15m, customerAfterRefund.CurrentBalance); // 30 - 15

        var creditEntry = await context.CustomerLedgerEntries.AsNoTracking()
            .SingleAsync(e => e.CustomerId == customerId && e.Type == CustomerLedgerEntryType.RefundCredit);
        Assert.Equal(-15m, creditEntry.Amount);
        Assert.Equal(15m, creditEntry.BalanceAfter);
        Assert.Equal("Refund", creditEntry.ReferenceType);
        Assert.Equal(refund.Id, creditEntry.ReferenceId);
    }

    [Fact]
    public async Task RefundSale_ApplyToBalanceExceedingRefundTotal_ThrowsConflict()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync(sellingPrice: 10m);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);
        var saleItemId = sale.Items.Single().Id;
        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);

        // Refunding 1 unit ($10), but trying to apply $15 to the balance - more than the refund itself.
        await Assert.ThrowsAsync<ConflictException>(() => new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(
                sale.Id, [new RefundLineInput(saleItemId, 1)], "Too much applied",
                ApplyToBalance: 15m, CustomerBalanceRowVersion: customer.RowVersion),
            CancellationToken.None));
    }

    [Fact]
    public async Task RefundSale_ApplyToBalanceExceedingCurrentBalance_ThrowsConflict()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync(sellingPrice: 10m);

        // Fully paid sale - customer owes nothing.
        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 50m, null)], CustomerId: customerId),
            CancellationToken.None);
        var saleItemId = sale.Items.Single().Id;
        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(0m, customer.CurrentBalance);

        await Assert.ThrowsAsync<ConflictException>(() => new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(
                sale.Id, [new RefundLineInput(saleItemId, 1)], "Nothing owed",
                ApplyToBalance: 5m, CustomerBalanceRowVersion: customer.RowVersion),
            CancellationToken.None));
    }

    [Fact]
    public async Task RefundSale_StaleCustomerBalanceRowVersion_ThrowsConflict()
    {
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync(sellingPrice: 10m);

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);
        var saleItemId = sale.Items.Single().Id;

        // Simulates the cashier's screen being built from stale data - a RowVersion that
        // doesn't match what's actually in the database right now (0 instead of the real value).
        await Assert.ThrowsAsync<ConflictException>(() => new RefundSaleCommandHandler(context, owner).Handle(
            new RefundSaleCommand(
                sale.Id, [new RefundLineInput(saleItemId, 1)], "Stale split",
                ApplyToBalance: 5m, CustomerBalanceRowVersion: -1),
            CancellationToken.None));
    }

    [Fact]
    public async Task VoidSale_OnCreditSale_ReversesChargeAndRestoresBalanceToZero()
    {
        // Regression test: voiding a credit sale previously left the customer permanently
        // owing money for a transaction that no longer existed - stock was restored but the
        // Charge ledger entry and CurrentBalance were untouched.
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 20m, null)], // 30 short of the 50 total
                CustomerId: customerId, AllowCredit: true),
            CancellationToken.None);

        var customerAfterSale = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(30m, customerAfterSale.CurrentBalance);

        await new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "Rang up wrong item"), CancellationToken.None);

        var customerAfterVoid = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(0m, customerAfterVoid.CurrentBalance);

        var entries = (await context.CustomerLedgerEntries.AsNoTracking()
            .Where(e => e.CustomerId == customerId).ToListAsync())
            .OrderBy(e => e.CreatedAt).ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal(CustomerLedgerEntryType.Charge, entries[0].Type);
        Assert.Equal(30m, entries[0].Amount);
        Assert.Equal(CustomerLedgerEntryType.ChargeReversal, entries[1].Type);
        Assert.Equal(-30m, entries[1].Amount);
        Assert.Equal(0m, entries[1].BalanceAfter);
        Assert.Equal("Sale", entries[1].ReferenceType);
        Assert.Equal(sale.Id, entries[1].ReferenceId);
    }

    [Fact]
    public async Task VoidSale_WithCustomerButNoCreditCharge_DoesNotTouchBalance()
    {
        // A customer attached to a fully-paid sale has no Charge entry to reverse - voiding it
        // must not fabricate one or alter the balance.
        var (seeded, context, owner, productId, customerId) = await SeedWithProductAndCustomerAsync();

        var sale = await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(
                seeded.BranchId, [new SaleLineInput(productId, 5, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 50m, null)], CustomerId: customerId),
            CancellationToken.None);

        await new VoidSaleCommandHandler(context, owner).Handle(new VoidSaleCommand(sale.Id, "Customer changed mind"), CancellationToken.None);

        var customer = await context.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
        Assert.Equal(0m, customer.CurrentBalance);
        Assert.Empty(await context.CustomerLedgerEntries.AsNoTracking().Where(e => e.CustomerId == customerId).ToListAsync());
    }

    public void Dispose() => _db.Dispose();
}
