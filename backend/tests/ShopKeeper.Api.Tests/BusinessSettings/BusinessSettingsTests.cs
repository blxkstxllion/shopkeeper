namespace ShopKeeper.Api.Tests.BusinessSettings;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.BusinessSettings.Commands;
using ShopKeeper.Application.BusinessSettings.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;

public class BusinessSettingsTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task GetBusinessSettings_ReturnsProfileAndTaxFields()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        var settings = await new GetBusinessSettingsQueryHandler(context, owner).Handle(new GetBusinessSettingsQuery(), CancellationToken.None);

        Assert.Equal("Ama's Shop", settings.Name);
        Assert.Equal("GHS", settings.CurrencyCode);
        Assert.Equal("Ghana", settings.Country);
        Assert.Equal("green", settings.ColorTheme);
    }

    [Fact]
    public async Task UpdateBusinessProfile_PersistsNameLegalNameTimeZoneAndColorTheme()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateBusinessProfileCommandHandler(context, owner).Handle(
            new UpdateBusinessProfileCommand("Ama's Superstore", "Ama Owusu Enterprises", "Africa/Lagos", "blue"), CancellationToken.None);

        var business = await context.Businesses.SingleAsync(b => b.Id == seeded.BusinessId);
        Assert.Equal("Ama's Superstore", business.Name);
        Assert.Equal("Ama Owusu Enterprises", business.LegalName);
        Assert.Equal("Africa/Lagos", business.TimeZone);
        Assert.Equal("blue", business.ColorTheme);
    }

    [Fact]
    public async Task UpdateBusinessProfileValidator_RejectsUnknownColorTheme()
    {
        var validator = new UpdateBusinessProfileCommandValidator();

        var result = await validator.ValidateAsync(new UpdateBusinessProfileCommand("Shop", null, "Africa/Accra", "purple"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateBusinessProfileCommand.ColorTheme));
    }

    [Fact]
    public async Task UpdateTaxSettings_PersistsRateAndInclusivity()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);

        await new UpdateTaxSettingsCommandHandler(context, owner).Handle(
            new UpdateTaxSettingsCommand(true, "TIN-12345", 12.5m, false), CancellationToken.None);

        var setting = await context.BusinessSettings.SingleAsync(s => s.BusinessId == seeded.BusinessId);
        Assert.True(setting.TaxEnabled);
        Assert.Equal("TIN-12345", setting.TaxIdNumber);
        Assert.Equal(12.5m, setting.TaxRatePercent);
        Assert.False(setting.TaxInclusivePricing);
    }

    [Fact]
    public async Task UpdateBusinessProfile_UserWithoutSettingsManagePermission_ThrowsForbidden()
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

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new UpdateBusinessProfileCommandHandler(context, cashier).Handle(
            new UpdateBusinessProfileCommand("Hacked Name", null, "Africa/Accra", "green"), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
