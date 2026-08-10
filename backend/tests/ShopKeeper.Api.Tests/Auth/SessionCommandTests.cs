namespace ShopKeeper.Api.Tests.Auth;

using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Auth.Queries;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Infrastructure.Identity;

public class SessionCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    [Fact]
    public async Task GetActiveSessions_MarksTheMatchingTokenAsCurrent()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var first = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("sessions@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1", "Chrome/Windows"), CancellationToken.None);
        currentUser.UserId = first.User.Id;

        var second = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("sessions@shop.test", "Passw0rd!", null, "10.0.0.1", "Safari/iPhone"), CancellationToken.None);

        var sessions = await new GetActiveSessionsQueryHandler(context, currentUser, _jwt).Handle(
            new GetActiveSessionsQuery(second.Auth!.RefreshToken), CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.IsCurrent);
        Assert.Contains(sessions, s => s.UserAgent == "Safari/iPhone" && s.IsCurrent);
        Assert.Contains(sessions, s => s.UserAgent == "Chrome/Windows" && !s.IsCurrent);
    }

    [Fact]
    public async Task RevokeSession_RemovesItFromActiveSessions()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("revoke@shop.test", "Passw0rd!", "Ama", "Owusu", null, "Device A"), CancellationToken.None);
        currentUser.UserId = registerResult.User.Id;

        var login2 = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("revoke@shop.test", "Passw0rd!", null, null, "Device B"), CancellationToken.None);

        var sessionsBefore = await new GetActiveSessionsQueryHandler(context, currentUser, _jwt).Handle(
            new GetActiveSessionsQuery(null), CancellationToken.None);
        var deviceASessionId = sessionsBefore.Single(s => s.UserAgent == "Device A").Id;

        await new RevokeSessionCommandHandler(context, currentUser).Handle(new RevokeSessionCommand(deviceASessionId), CancellationToken.None);

        var sessionsAfter = await new GetActiveSessionsQueryHandler(context, currentUser, _jwt).Handle(
            new GetActiveSessionsQuery(login2.Auth!.RefreshToken), CancellationToken.None);

        Assert.Single(sessionsAfter);
        Assert.Equal("Device B", sessionsAfter[0].UserAgent);
    }

    [Fact]
    public async Task RevokeSession_BelongingToAnotherUser_ThrowsNotFound()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var userA = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("usera@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        currentUser.UserId = userA.User.Id;
        var userASessions = await new GetActiveSessionsQueryHandler(context, currentUser, _jwt).Handle(
            new GetActiveSessionsQuery(null), CancellationToken.None);
        var userASessionId = userASessions.Single().Id;

        var userB = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("userb@shop.test", "Passw0rd!", "Kofi", "Mensah", null), CancellationToken.None);
        currentUser.UserId = userB.User.Id; // now acting as user B

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RevokeSessionCommandHandler(context, currentUser).Handle(new RevokeSessionCommand(userASessionId), CancellationToken.None));
    }

    [Fact]
    public async Task RevokeAllOtherSessions_KeepsCurrentSessionActive()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("revokeall@shop.test", "Passw0rd!", "Ama", "Owusu", null, "Device A"), CancellationToken.None);
        currentUser.UserId = registerResult.User.Id;

        await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("revokeall@shop.test", "Passw0rd!", null, null, "Device B"), CancellationToken.None);
        var login3 = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("revokeall@shop.test", "Passw0rd!", null, null, "Device C"), CancellationToken.None);

        await new RevokeAllOtherSessionsCommandHandler(context, currentUser, _jwt).Handle(
            new RevokeAllOtherSessionsCommand(login3.Auth!.RefreshToken), CancellationToken.None);

        var remaining = await new GetActiveSessionsQueryHandler(context, currentUser, _jwt).Handle(
            new GetActiveSessionsQuery(login3.Auth!.RefreshToken), CancellationToken.None);

        Assert.Single(remaining);
        Assert.True(remaining[0].IsCurrent);
        Assert.Equal("Device C", remaining[0].UserAgent);
    }

    public void Dispose() => _db.Dispose();
}
