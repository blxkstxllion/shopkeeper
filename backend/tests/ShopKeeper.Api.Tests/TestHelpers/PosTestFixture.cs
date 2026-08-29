namespace ShopKeeper.Api.Tests.TestHelpers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>Seeds a fully onboarded business (owner, roles/permissions, one branch) so
/// Phase 2 tests can start from a realistic tenant instead of re-deriving onboarding
/// plumbing in every test class.</summary>
public static class PosTestFixture
{
    public static readonly JwtSettings JwtTestSettings = new()
    {
        Issuer = "ShopKeeper.Tests",
        Audience = "ShopKeeper.Tests",
        Secret = "test-secret-at-least-32-bytes-long-for-hmac-sha256",
    };

    public record SeededBusiness(Guid BusinessId, Guid BranchId, Guid OwnerId);

    public static async Task<SeededBusiness> SeedAsync(
        ITestDatabase db, BcryptPasswordHasher hasher, JwtTokenService jwt, string ownerEmail = "owner@shop.test")
    {
        var setupUser = new TestCurrentUserService();
        var context = db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(context, jwt);

        var registerResult = await new RegisterCommandHandler(context, hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand(ownerEmail, "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var business = await new CompleteOnboardingCommandHandler(context, tokenIssuer).Handle(
            new CompleteOnboardingCommand(
                registerResult.User.Id, "Ama's Shop", BusinessType.Retail, null, "Ghana", "GHS", null,
                false, 0, true, [], "Main Store", null, null, null),
            CancellationToken.None);

        return new SeededBusiness(business.Id, business.FirstBranchId, registerResult.User.Id);
    }

    /// <summary>An owner-scoped ICurrentUserService for the seeded business (full permissions).</summary>
    public static TestCurrentUserService AsOwner(this SeededBusiness seeded) => new()
    {
        UserId = seeded.OwnerId,
        BusinessId = seeded.BusinessId,
        BranchId = null,
        IsOwner = true,
    };
}
