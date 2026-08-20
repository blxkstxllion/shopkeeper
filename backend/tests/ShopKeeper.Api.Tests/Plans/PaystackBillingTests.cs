namespace ShopKeeper.Api.Tests.Plans;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Plans.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class PaystackBillingTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task ProcessWebhook_DuplicateDelivery_ProcessesOnce()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var context = _db.CreateContext(seeded.AsOwner());
        var seededBusiness = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        seededBusiness.PaystackCustomerCode = "CUS_dup_test";
        await context.SaveChangesAsync();

        var rawBody = """
            {"event":"subscription.create","data":{"subscription_code":"SUB_dup","email_token":"tok_dup",
            "status":"active","customer":{"customer_code":"CUS_dup_test"},"plan":{"plan_code":"PLN_business"},
            "next_payment_date":"2027-01-01T00:00:00.000Z"}}
            """;

        var handler = new ProcessPaystackWebhookCommandHandler(context, NullLogger<ProcessPaystackWebhookCommandHandler>.Instance);
        await handler.Handle(new ProcessPaystackWebhookCommand(rawBody), CancellationToken.None);
        await handler.Handle(new ProcessPaystackWebhookCommand(rawBody), CancellationToken.None);

        Assert.Equal(1, await context.PaystackWebhookEvents.CountAsync());
        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal("SUB_dup", business.PaystackSubscriptionCode);
        Assert.Equal("active", business.PaystackSubscriptionStatus);
    }

    [Fact]
    public async Task SetPlanTier_DowngradeToFree_WithActiveSubscription_CallsDisable()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var seededBusiness = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        seededBusiness.PlanTier = PlanTier.Business;
        seededBusiness.PaystackSubscriptionCode = "SUB_active";
        seededBusiness.PaystackSubscriptionEmailToken = "tok_active";
        await context.SaveChangesAsync();

        var paystack = new TestPaystackClient { IsConfigured = true };
        await new SetPlanTierCommandHandler(context, owner, paystack).Handle(
            new SetPlanTierCommand(PlanTier.Free), CancellationToken.None);

        Assert.Equal(("SUB_active", "tok_active"), paystack.LastDisabledSubscription);
        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(PlanTier.Free, business.PlanTier);
        Assert.Equal("cancelled", business.PaystackSubscriptionStatus);
    }

    [Fact]
    public async Task SetPlanTier_WhenPaystackConfigured_RejectsDirectPaidTierChange()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var paystack = new TestPaystackClient { IsConfigured = true };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new SetPlanTierCommandHandler(context, owner, paystack).Handle(
                new SetPlanTierCommand(PlanTier.Enterprise), CancellationToken.None));

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(PlanTier.Free, business.PlanTier); // unchanged
    }

    [Fact]
    public async Task InitiateCheckout_NonOwner_ThrowsForbidden()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var nonOwner = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BusinessId = seeded.BusinessId,
            BranchId = seeded.BranchId,
            IsOwner = false,
        };
        var context = _db.CreateContext(nonOwner);
        var paystack = new TestPaystackClient { IsConfigured = true };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new InitiateCheckoutCommandHandler(context, nonOwner, paystack).Handle(
                new InitiateCheckoutCommand(PlanTier.Business), CancellationToken.None));
    }

    [Fact]
    public async Task InitiateCheckout_FreeTier_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var paystack = new TestPaystackClient { IsConfigured = true };

        await Assert.ThrowsAsync<ConflictException>(() =>
            new InitiateCheckoutCommandHandler(context, owner, paystack).Handle(
                new InitiateCheckoutCommand(PlanTier.Free), CancellationToken.None));
    }

    [Fact]
    public async Task InitiateCheckout_PaystackNotConfigured_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var paystack = new TestPaystackClient { IsConfigured = false };

        await Assert.ThrowsAsync<ConflictException>(() =>
            new InitiateCheckoutCommandHandler(context, owner, paystack).Handle(
                new InitiateCheckoutCommand(PlanTier.Business), CancellationToken.None));
    }

    [Fact]
    public async Task VerifyCheckout_MismatchedBusinessInReference_ThrowsForbidden()
    {
        var businessA = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-a@shop.test");
        var businessB = await PosTestFixture.SeedAsync(_db, _hasher, _jwt, "owner-b@shop.test");
        var ownerA = businessA.AsOwner();
        var context = _db.CreateContext(ownerA);
        var paystack = new TestPaystackClient { IsConfigured = true };

        // A reference genuinely encoding business B, but the call is made as business A's owner.
        var foreignReference = $"chk_{businessB.BusinessId:N}_Business_{Guid.NewGuid():N}";

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new VerifyCheckoutCommandHandler(context, ownerA, paystack).Handle(
                new VerifyCheckoutCommand(foreignReference), CancellationToken.None));
    }

    [Fact]
    public async Task VerifyCheckout_MalformedReference_ThrowsConflict()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var paystack = new TestPaystackClient { IsConfigured = true };

        await Assert.ThrowsAsync<ConflictException>(() =>
            new VerifyCheckoutCommandHandler(context, owner, paystack).Handle(
                new VerifyCheckoutCommand("not-a-real-reference"), CancellationToken.None));
    }

    [Fact]
    public async Task VerifyCheckout_SuccessfulPayment_ActivatesTierAndPersistsSubscription()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var reference = $"chk_{seeded.BusinessId:N}_Business_{Guid.NewGuid():N}";
        var paystack = new TestPaystackClient
        {
            IsConfigured = true,
            NextVerifyResult = new(true, "success", "owner@shop.test", "CUS_verify_test"),
            NextSubscriptionInfo = new("SUB_verify", "tok_verify", "active", DateTimeOffset.UtcNow.AddDays(30)),
        };

        var result = await new VerifyCheckoutCommandHandler(context, owner, paystack).Handle(
            new VerifyCheckoutCommand(reference), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(PlanTier.Business, result.NewTier);

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal(PlanTier.Business, business.PlanTier);
        Assert.Equal("CUS_verify_test", business.PaystackCustomerCode);
        Assert.Equal("SUB_verify", business.PaystackSubscriptionCode);
    }

    public void Dispose() => _db.Dispose();
}
