namespace ShopKeeper.Api.Tests.Auth;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Auth.Queries;
using ShopKeeper.Application.Businesses.Queries;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// RequireVerifiedEmailBehavior runs as a MediatR pipeline behavior - like
/// RequirePlanTierBehaviorTests, these go through a real ISender rather than constructing
/// handlers by hand, which is the only way this behavior actually runs at all.
///
/// PosTestFixture.SeedAsync marks its owner as already verified (a "fully onboarded
/// business" is realistically past this), so these tests seed their own unverified user
/// directly rather than relying on the shared fixture.
/// </summary>
public class RequireVerifiedEmailBehaviorTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private ISender BuildSender(IAppDbContext context, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(context);
        services.AddSingleton(currentUser);
        services.AddSingleton<IJwtTokenService>(_jwt); // CompleteOnboardingCommand needs TokenIssuer, which needs this
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task UnverifiedAndEnforced_BlocksNonAuthRequest()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId!.Value);
        user.IsEmailVerified = false;
        user.EmailVerificationEnforced = true;
        await context.SaveChangesAsync(CancellationToken.None);

        var sender = BuildSender(context, owner);

        var ex = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sender.Send(new GetBranchesQuery(), CancellationToken.None));
        Assert.Contains("verify your email", ex.Message);
    }

    [Fact]
    public async Task UnverifiedButNotEnforced_GrandfatheredAccount_Succeeds()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId!.Value);
        user.IsEmailVerified = false;
        user.EmailVerificationEnforced = false; // pre-enforcement account
        await context.SaveChangesAsync(CancellationToken.None);

        var sender = BuildSender(context, owner);

        var branches = await sender.Send(new GetBranchesQuery(), CancellationToken.None);
        Assert.NotNull(branches);
    }

    [Fact]
    public async Task UnverifiedAndEnforced_AuthRequestsStillWork()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId!.Value);
        user.IsEmailVerified = false;
        user.EmailVerificationEnforced = true;
        await context.SaveChangesAsync(CancellationToken.None);

        var sender = BuildSender(context, owner);

        // GetCurrentUserQuery lives in ShopKeeper.Application.Auth.Queries - the frontend
        // needs this to work even while blocked, to know it's blocked in the first place.
        var me = await sender.Send(new GetCurrentUserQuery(owner.UserId!.Value), CancellationToken.None);
        Assert.False(me.IsEmailVerified);
        Assert.True(me.MustVerifyEmail);
    }

    [Fact]
    public async Task Verified_Succeeds_EvenWhenEnforced()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var user = await context.Users.SingleAsync(u => u.Id == owner.UserId!.Value);
        user.IsEmailVerified = true;
        user.EmailVerificationEnforced = true;
        await context.SaveChangesAsync(CancellationToken.None);

        var sender = BuildSender(context, owner);

        var branches = await sender.Send(new GetBranchesQuery(), CancellationToken.None);
        Assert.NotNull(branches);
    }

    [Fact]
    public async Task UnverifiedAndEnforced_CanStillCompleteOnboarding()
    {
        // A brand-new registration is unverified-and-enforced by default (RegisterCommand) -
        // if onboarding weren't exempt, nobody could ever get past this screen: the frontend's
        // own RequireVerifiedEmail route guard only wraps /app/*, not /onboarding, so a user
        // who's never verified would see a working onboarding UI whose final submit always 403s.
        var context = _db.CreateContext(new TestCurrentUserService());
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerHandler = new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender());
        var registered = await registerHandler.Handle(
            new RegisterCommand("newcomer@shop.test", "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Id == registered.User.Id);
        Assert.True(user.EmailVerificationEnforced);
        Assert.False(user.IsEmailVerified);

        var currentUser = new TestCurrentUserService { UserId = user.Id };
        var sender = BuildSender(context, currentUser);

        var business = await sender.Send(new CompleteOnboardingCommand(
            OwnerUserId: user.Id,
            BusinessName: "Kofi's Shop",
            BusinessType: BusinessType.Retail,
            BusinessTypeOther: null,
            Country: "Ghana",
            CurrencyCode: "GHS",
            LogoUrl: null,
            TaxEnabled: false,
            TaxRatePercent: 0,
            TaxInclusivePricing: false,
            Goals: [BusinessGoal.IncreaseProfit],
            FirstBranchName: "Main Store",
            FirstBranchAddress: null,
            FirstBranchCity: null,
            IpAddress: null), CancellationToken.None);

        Assert.Equal("Kofi's Shop", business.Name);
    }

    public void Dispose() => _db.Dispose();
}
