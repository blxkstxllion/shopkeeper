namespace ShopKeeper.Api.Tests.Advisor;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Advisor.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Expenses.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Identity;

public class AdvisorTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task RevenueThisMonth_ReflectsCurrentSales()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-1", null, null, null, null, 10m, 6m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.RevenueThisMonth, null), CancellationToken.None);

        Assert.Contains("GHS 50.00", answer.Answer);
    }

    [Fact]
    public async Task ProfitMargin_ComputesFromRealSale()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-2", null, null, null, null, 10m, 5m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.ProfitMargin, null), CancellationToken.None);

        Assert.Contains("50", answer.Answer); // (10-5)/10 = 50% gross margin
    }

    [Fact]
    public async Task ProfitMargin_WithNoRevenue_SaysNothingToCalculate()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.ProfitMargin, null), CancellationToken.None);

        Assert.Contains("no revenue", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LowStock_ListsLowAndOutOfStockProducts()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Almost Gone", "SKU-ADV-3", null, null, null, null, 10m, 5m, 5, 10, true, 2, seeded.BranchId),
            CancellationToken.None);
        await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("All Gone", "SKU-ADV-4", null, null, null, null, 10m, 5m, 5, 10, true, 0, seeded.BranchId),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.LowStock, null), CancellationToken.None);

        Assert.Contains("Almost Gone", answer.Answer);
        Assert.Contains("All Gone", answer.Answer);
    }

    [Fact]
    public async Task LowStock_WhenHealthy_SaysSo()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Plenty", "SKU-ADV-5", null, null, null, null, 10m, 5m, 5, 10, true, 100, seeded.BranchId),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.LowStock, null), CancellationToken.None);

        Assert.Contains("healthy", answer.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BestSellingProduct_ReflectsTopSeller()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var top = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Popular", "SKU-ADV-6", null, null, null, null, 10m, 5m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        var other = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Slow Mover", "SKU-ADV-7", null, null, null, null, 10m, 5m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(top.Id, 10, 0), new SaleLineInput(other.Id, 1, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 110m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.BestSellingProduct, null), CancellationToken.None);

        Assert.Contains("Popular", answer.Answer);
    }

    [Fact]
    public async Task WorstPerformingProduct_ReflectsLowestProfitProduct()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var good = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("High Margin", "SKU-ADV-8", null, null, null, null, 20m, 5m, 5, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        var bad = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Low Margin", "SKU-ADV-9", null, null, null, null, 10m, 9m, 5, 10, true, 10, seeded.BranchId),
            CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(good.Id, 2, 0), new SaleLineInput(bad.Id, 2, 0)], 0,
                [new SalePaymentInput(PaymentMethod.Cash, 60m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.WorstPerformingProduct, null), CancellationToken.None);

        Assert.Contains("Low Margin", answer.Answer);
    }

    [Fact]
    public async Task BranchComparison_SingleBranch_DescribesOnlyBranch()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-9B", null, null, null, null, 10m, 5m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 2, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 20m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.BranchComparison, null), CancellationToken.None);

        Assert.Contains("Main Store", answer.Answer);
    }

    [Fact]
    public async Task BranchComparison_NoSalesYet_SaysSo()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.BranchComparison, null), CancellationToken.None);

        Assert.Contains("No branch", answer.Answer);
    }

    [Fact]
    public async Task BranchComparison_ScopedUser_SaysNotAvailable()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var branchManager = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = ["ai_consultant:use", "reports:view", "sales:view"],
        };

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, branchManager), context, branchManager, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.BranchComparison, null), CancellationToken.None);

        Assert.Contains("scoped to a single branch", answer.Answer);
    }

    [Fact]
    public async Task BranchComparison_MultipleBranches_RanksByProfit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var branchB = new Branch { BusinessId = seeded.BusinessId, Name = "Branch B", Code = "B2", Country = "Ghana" };
        context.Branches.Add(branchB);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-10", null, null, null, null, 10m, 6m, 5, 10, true, 10, seeded.BranchId),
            CancellationToken.None);
        context.ProductStocks.Add(new ProductStock { BusinessId = seeded.BusinessId, ProductId = product.Id, BranchId = branchB.Id, QuantityOnHand = 10 });
        await context.SaveChangesAsync(CancellationToken.None);

        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(branchB.Id, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.BranchComparison, null), CancellationToken.None);

        Assert.Contains("Main Store", answer.Answer);
        Assert.Contains("Branch B", answer.Answer);
    }

    [Fact]
    public async Task TopExpenseCategories_ReflectsRealExpenses()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var rent = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, rent.Id, 800m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.TopExpenseCategories, null), CancellationToken.None);

        Assert.Contains("Rent", answer.Answer);
        Assert.Contains("GHS 800.00", answer.Answer);
    }

    [Fact]
    public async Task TopExpenseCategories_WithNoExpenses_SaysSo()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.TopExpenseCategories, null), CancellationToken.None);

        Assert.Contains("No expenses", answer.Answer);
    }

    [Fact]
    public async Task AmIProfitable_TrueWhenNetProfitPositive()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-11", null, null, null, null, 10m, 5m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.AmIProfitable, null), CancellationToken.None);

        Assert.StartsWith("Yes", answer.Answer);
    }

    [Fact]
    public async Task AmIProfitable_FalseWhenExpensesExceedProfit()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-12", null, null, null, null, 10m, 9m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 1, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 10m, null)]),
            CancellationToken.None);

        var rent = await new CreateExpenseCategoryCommandHandler(context, owner).Handle(
            new CreateExpenseCategoryCommand("Rent", null), CancellationToken.None);
        await new CreateExpenseCommandHandler(context, owner).Handle(
            new CreateExpenseCommand(seeded.BranchId, rent.Id, 500m, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.AmIProfitable, null), CancellationToken.None);

        Assert.StartsWith("Not yet", answer.Answer);
    }

    [Fact]
    public async Task GetAdvisorQuestions_ReturnsAllEightWithUniqueIds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var handler = new GetAdvisorQuestionsQueryHandler(owner);
        var questions = await handler.Handle(new GetAdvisorQuestionsQuery(), CancellationToken.None);

        Assert.Equal(8, questions.Count);
        Assert.Equal(8, questions.Select(q => q.Id).Distinct().Count());
        Assert.All(questions, q => Assert.False(string.IsNullOrWhiteSpace(q.Label)));
    }

    [Fact]
    public async Task AiConsultantUse_RequiredForQuestionsAndAnswer()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());

        var cashier = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
            PermissionsList = ["sales:view", "sales:create"], // no ai_consultant:use
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetAdvisorQuestionsQueryHandler(cashier).Handle(new GetAdvisorQuestionsQuery(), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new GetAdvisorAnswerQueryHandler(new TestSender(context, cashier), context, cashier, new PassthroughAdvisorNarrator()).Handle(
                new GetAdvisorAnswerQuery(AdvisorQuestionId.RevenueThisMonth, null), CancellationToken.None));
    }

    [Fact]
    public async Task Answer_NeverReflectsAnotherBusinessData()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-a@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-b@shop.test");
        var ownerA = businessA.AsOwner();
        var ownerB = businessB.AsOwner();

        var contextB = _db.CreateContext(ownerB);
        var productB = await new CreateProductCommandHandler(contextB, ownerB, new PlanLimitService(contextB)).Handle(
            new CreateProductCommand("Business B Widget", "SKU-ADV-B", null, null, null, null, 999m, 1m, 5, 10, true, 50, businessB.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(contextB, ownerB, new NotificationDispatcher(contextB)).Handle(
            new CreateSaleCommand(businessB.BranchId, [new SaleLineInput(productB.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 4995m, null)]),
            CancellationToken.None);

        var contextA = _db.CreateContext(ownerA);
        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(contextA, ownerA), contextA, ownerA, new PassthroughAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.RevenueThisMonth, null), CancellationToken.None);

        Assert.Contains("GHS 0.00", answer.Answer);
        Assert.DoesNotContain("4,995", answer.Answer);
    }

    /// <summary>Always throws, simulating a Claude API outage/error - proves
    /// GetAdvisorAnswerQueryHandler's try/catch around narration actually protects the feature,
    /// not just the happy path.</summary>
    private class ThrowingAdvisorNarrator : IAdvisorNarrator
    {
        public Task<string> NarrateAsync(string questionLabel, string groundedAnswer, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated Anthropic API outage.");
    }

    [Fact]
    public async Task Answer_WhenNarratorThrows_FallsBackToGroundedAnswer()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ADV-13", null, null, null, null, 10m, 6m, 5, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        var handler = new GetAdvisorAnswerQueryHandler(new TestSender(context, owner), context, owner, new ThrowingAdvisorNarrator());
        var answer = await handler.Handle(new GetAdvisorAnswerQuery(AdvisorQuestionId.RevenueThisMonth, null), CancellationToken.None);

        Assert.Contains("GHS 50.00", answer.Answer);
    }

    public void Dispose() => _db.Dispose();
}
