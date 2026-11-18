namespace ShopKeeper.Api.Tests.LocalEdition;

using Microsoft.EntityFrameworkCore;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.LocalEdition.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Infrastructure.Identity;

public class UnlockLocalAppCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();

    private async Task<ShopKeeper.Infrastructure.Persistence.AppDbContext> SeedAsync(string pin = "1234")
    {
        var context = _db.CreateContext(new TestCurrentUserService());
        context.LocalAppSettings.Add(new LocalAppSettings
        {
            StoreName = "Ama's Shop",
            PinHash = _hasher.Hash(pin),
            SetupCompletedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Unlock_WithCorrectPin_ReturnsTrue()
    {
        var context = await SeedAsync("1234");
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        var result = await handler.Handle(new UnlockLocalAppCommand("1234"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Unlock_WithWrongPin_ReturnsFalse()
    {
        var context = await SeedAsync("1234");
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        var result = await handler.Handle(new UnlockLocalAppCommand("9999"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Unlock_BeforeSetupCompleted_ThrowsAuthenticationException()
    {
        var context = _db.CreateContext(new TestCurrentUserService());
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            handler.Handle(new UnlockLocalAppCommand("1234"), CancellationToken.None));
    }

    [Fact]
    public async Task Unlock_FiveConsecutiveWrongPins_LocksEvenTheCorrectPinOnTheSixthAttempt()
    {
        // A 4-digit PIN is only 10,000 combinations - this is the whole reason the lockout
        // exists. Without it, an unattended app could be brute-forced in well under a minute.
        var context = await SeedAsync("1234");
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        for (var i = 0; i < 5; i++)
        {
            Assert.False(await handler.Handle(new UnlockLocalAppCommand("0000"), CancellationToken.None));
        }

        var result = await handler.Handle(new UnlockLocalAppCommand("1234"), CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task Unlock_CorrectPinAfterSomeFailures_ResetsFailedAttemptCounter()
    {
        var context = await SeedAsync("1234");
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        for (var i = 0; i < 3; i++)
        {
            Assert.False(await handler.Handle(new UnlockLocalAppCommand("0000"), CancellationToken.None));
        }

        Assert.True(await handler.Handle(new UnlockLocalAppCommand("1234"), CancellationToken.None));

        var settings = await context.LocalAppSettings.SingleAsync();
        Assert.Equal(0, settings.FailedUnlockAttempts);
        Assert.Null(settings.UnlockLockedUntil);
    }

    [Fact]
    public async Task Unlock_AfterLockoutWindowExpires_SucceedsAgain()
    {
        var context = await SeedAsync("1234");
        var handler = new UnlockLocalAppCommandHandler(context, _hasher);

        for (var i = 0; i < 5; i++)
        {
            await handler.Handle(new UnlockLocalAppCommand("0000"), CancellationToken.None);
        }

        var settings = await context.LocalAppSettings.SingleAsync();
        settings.UnlockLockedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await handler.Handle(new UnlockLocalAppCommand("1234"), CancellationToken.None);
        Assert.True(result);
    }

    public void Dispose() => _db.Dispose();
}
