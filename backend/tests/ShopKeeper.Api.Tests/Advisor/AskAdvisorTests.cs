namespace ShopKeeper.Api.Tests.Advisor;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Advisor.Commands;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Sales.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// AskAdvisorCommand tests, separate from AdvisorTests.cs since these exercise the tool-calling
/// round trip with a scripted TestAdvisorConversationClient rather than the narration-only path.
/// </summary>
public class AskAdvisorTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private const string FallbackAnswer =
        "I can only answer questions about revenue, profit margin, stock levels, top/worst " +
        "products, branch comparison, expenses, and profitability right now - try one of the " +
        "quick questions above, or rephrase.";

    private async Task<(TestCurrentUserService owner, ShopKeeper.Infrastructure.Persistence.AppDbContext context)> SeedWithSaleAsync()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var product = await new CreateProductCommandHandler(context, owner, new PlanLimitService(context)).Handle(
            new CreateProductCommand("Widget", "SKU-ASK-1", null, null, null, null, 10m, 6m, 10, true, 50, seeded.BranchId),
            CancellationToken.None);
        await new CreateSaleCommandHandler(context, owner, new NotificationDispatcher(context)).Handle(
            new CreateSaleCommand(seeded.BranchId, [new SaleLineInput(product.Id, 5, 0)], 0, [new SalePaymentInput(PaymentMethod.Cash, 50m, null)]),
            CancellationToken.None);

        return (owner, context);
    }

    [Fact]
    public async Task Ask_ToolUseRoundTrip_PassesRealGroundedNumberToSecondClaudeCall()
    {
        var (owner, context) = await SeedWithSaleAsync();
        var client = new TestAdvisorConversationClient();
        client.Responses.Enqueue(new ClaudeTurn(null, [new ClaudeToolUse("toolu_1", nameof(AdvisorQuestionId.RevenueThisMonth))]));
        client.Responses.Enqueue(new ClaudeTurn("Revenue is looking good this month.", []));

        var handler = new AskAdvisorCommandHandler(client, new AdvisorCalculations(new TestSender(context, owner), context, owner), owner);
        var result = await handler.Handle(new AskAdvisorCommand("how's my revenue", null), CancellationToken.None);

        Assert.Equal("Revenue is looking good this month.", result.Answer);
        Assert.Equal(2, client.CallCount);

        // The second Claude call's tool_result must carry the real number AdvisorCalculations
        // computed from the seeded sale - proves Claude only narrates, it never computes.
        var secondCallToolResult = client.CallHistory[1].Last().ToolResults!.Single();
        Assert.Contains("GHS 50.00", secondCallToolResult.Content);
    }

    [Fact]
    public async Task Ask_UnrecognizedToolName_SendsSafePlaceholderInsteadOfCrashing()
    {
        var (owner, context) = await SeedWithSaleAsync();
        var client = new TestAdvisorConversationClient();
        client.Responses.Enqueue(new ClaudeTurn(null, [new ClaudeToolUse("toolu_1", "SomeToolClaudeInvented")]));
        client.Responses.Enqueue(new ClaudeTurn("I don't have that information.", []));

        var handler = new AskAdvisorCommandHandler(client, new AdvisorCalculations(new TestSender(context, owner), context, owner), owner);
        var result = await handler.Handle(new AskAdvisorCommand("something unrelated", null), CancellationToken.None);

        Assert.Equal("I don't have that information.", result.Answer);
        var secondCallToolResult = client.CallHistory[1].Last().ToolResults!.Single();
        Assert.Equal("This topic isn't available.", secondCallToolResult.Content);
    }

    [Fact]
    public async Task Ask_WhenClaudeRequestsToolsTwice_FallsBackAfterOneRound()
    {
        var (owner, context) = await SeedWithSaleAsync();
        var client = new TestAdvisorConversationClient();
        client.Responses.Enqueue(new ClaudeTurn(null, [new ClaudeToolUse("toolu_1", nameof(AdvisorQuestionId.RevenueThisMonth))]));
        client.Responses.Enqueue(new ClaudeTurn(null, [new ClaudeToolUse("toolu_2", nameof(AdvisorQuestionId.ProfitMargin))])); // asks again - past the cap

        var handler = new AskAdvisorCommandHandler(client, new AdvisorCalculations(new TestSender(context, owner), context, owner), owner);
        var result = await handler.Handle(new AskAdvisorCommand("how's my business", null), CancellationToken.None);

        Assert.Equal(FallbackAnswer, result.Answer);
        Assert.Equal(2, client.CallCount); // never attempted a third round
    }

    [Fact]
    public async Task Ask_WhenClaudeUnavailable_FallsBackToSafeAnswer()
    {
        var (owner, context) = await SeedWithSaleAsync();
        var client = new UnavailableAdvisorConversationClient();

        var handler = new AskAdvisorCommandHandler(client, new AdvisorCalculations(new TestSender(context, owner), context, owner), owner);
        var result = await handler.Handle(new AskAdvisorCommand("how's my revenue", null), CancellationToken.None);

        Assert.Equal(FallbackAnswer, result.Answer);
    }

    [Fact]
    public async Task Ask_WhenHttpCallFails_FallsBackToSafeAnswer()
    {
        var (owner, context) = await SeedWithSaleAsync();
        var client = new TestAdvisorConversationClient { ThrowException = new InvalidOperationException("Anthropic API returned 500.") };

        var handler = new AskAdvisorCommandHandler(client, new AdvisorCalculations(new TestSender(context, owner), context, owner), owner);
        var result = await handler.Handle(new AskAdvisorCommand("how's my revenue", null), CancellationToken.None);

        Assert.Equal(FallbackAnswer, result.Answer);
    }

    public void Dispose() => _db.Dispose();
}
