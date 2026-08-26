namespace ShopKeeper.Api.Tests.Onboarding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Enums;
using ShopKeeper.Infrastructure.Identity;

public class OnboardingCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(new JwtSettings
    {
        Issuer = "ShopKeeper.Tests",
        Audience = "ShopKeeper.Tests",
        Secret = "test-secret-at-least-32-bytes-long-for-hmac-sha256",
    }));

    [Fact]
    public async Task CompleteOnboarding_SeedsAllSevenDefaultRoles_WithExpectedPermissions()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var owner = await RegisterOwner(context, tokenIssuer);

        var handler = new CompleteOnboardingCommandHandler(context, tokenIssuer);
        var business = await handler.Handle(BuildCommand(owner.Id), CancellationToken.None);

        var roles = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => r.BusinessId == business.Id)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .ToListAsync();

        Assert.Equal(DefaultRoles.RolePermissionKeys.Count, roles.Count);

        var ownerRole = Assert.Single(roles, r => r.Name == DefaultRoles.Owner);
        Assert.Equal(PermissionKeys.All.Count, ownerRole.RolePermissions.Count);

        var cashierRole = Assert.Single(roles, r => r.Name == DefaultRoles.Cashier);
        var cashierPermissionKeys = cashierRole.RolePermissions.Select(rp => rp.Permission.Key).ToHashSet();
        Assert.Contains(PermissionKeys.SalesCreate, cashierPermissionKeys);
        Assert.DoesNotContain(PermissionKeys.UsersManage, cashierPermissionKeys);
    }

    [Fact]
    public async Task CompleteOnboarding_CreatesOwnerMembershipAndMainBranch()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var owner = await RegisterOwner(context, tokenIssuer);

        var handler = new CompleteOnboardingCommandHandler(context, tokenIssuer);
        var business = await handler.Handle(BuildCommand(owner.Id), CancellationToken.None);

        var membership = await context.BusinessUsers
            .IgnoreQueryFilters()
            .Include(bu => bu.Role)
            .SingleAsync(bu => bu.BusinessId == business.Id && bu.UserId == owner.Id);

        Assert.True(membership.IsOwner);
        Assert.Equal(DefaultRoles.Owner, membership.Role.Name);
        Assert.Equal(BusinessUserStatus.Active, membership.Status);

        var branch = await context.Branches.IgnoreQueryFilters().SingleAsync(b => b.Id == business.FirstBranchId);
        Assert.True(branch.IsMainBranch);
        Assert.Equal("Main Store", branch.Name);
    }

    [Fact]
    public async Task CompleteOnboarding_IssuesAccessTokenScopedToNewBusiness()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var owner = await RegisterOwner(context, tokenIssuer);

        var handler = new CompleteOnboardingCommandHandler(context, tokenIssuer);
        var business = await handler.Handle(BuildCommand(owner.Id), CancellationToken.None);

        Assert.NotEmpty(business.AccessToken);
        Assert.True(business.OnboardingCompleted);
    }

    [Fact]
    public async Task CompleteOnboarding_DefaultsColorThemeToGreenWhenNotSpecified()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var owner = await RegisterOwner(context, tokenIssuer);

        var handler = new CompleteOnboardingCommandHandler(context, tokenIssuer);
        var business = await handler.Handle(BuildCommand(owner.Id), CancellationToken.None);

        Assert.Equal("green", business.ColorTheme);
    }

    [Fact]
    public async Task CompleteOnboarding_PersistsSuggestedColorThemeWhenSpecified()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var owner = await RegisterOwner(context, tokenIssuer);

        var handler = new CompleteOnboardingCommandHandler(context, tokenIssuer);
        var business = await handler.Handle(BuildCommand(owner.Id) with { ColorTheme = "red" }, CancellationToken.None);

        Assert.Equal("red", business.ColorTheme);
    }

    [Fact]
    public async Task CompleteOnboardingValidator_RejectsUnknownColorTheme()
    {
        var validator = new CompleteOnboardingCommandValidator();

        var result = await validator.ValidateAsync(BuildCommand(Guid.NewGuid()) with { ColorTheme = "purple" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CompleteOnboardingCommand.ColorTheme));
    }

    private async Task<Domain.Entities.User> RegisterOwner(
        Infrastructure.Persistence.AppDbContext context, TokenIssuer tokenIssuer)
    {
        var registerHandler = new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender());
        var result = await registerHandler.Handle(
            new RegisterCommand("founder@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);
        return await context.Users.SingleAsync(u => u.Id == result.User.Id);
    }

    private static CompleteOnboardingCommand BuildCommand(Guid ownerId) => new(
        OwnerUserId: ownerId,
        BusinessName: "Ama's Provisions",
        BusinessType: BusinessType.Retail,
        Country: "Ghana",
        CurrencyCode: "GHS",
        LogoUrl: null,
        TaxEnabled: true,
        TaxRatePercent: 12.5m,
        TaxInclusivePricing: true,
        Goals: [BusinessGoal.IncreaseProfit, BusinessGoal.TrackExpenses],
        FirstBranchName: "Main Store",
        FirstBranchAddress: "123 High Street",
        FirstBranchCity: "Accra",
        IpAddress: null);

    public void Dispose() => _db.Dispose();
}
