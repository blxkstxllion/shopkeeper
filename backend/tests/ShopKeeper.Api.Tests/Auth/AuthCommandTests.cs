namespace ShopKeeper.Api.Tests.Auth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Infrastructure.Identity;

public class AuthCommandTests : IDisposable
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
    public async Task Register_WithNewEmail_CreatesUserAndReturnsTokens()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), new TestEmailSender());

        var result = await handler.Handle(
            new RegisterCommand("owner@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1"), CancellationToken.None);

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("owner@shop.test", result.User.Email);
        Assert.Empty(result.User.Businesses); // no business created yet - onboarding comes next
    }

    [Fact]
    public async Task Register_WithNewEmail_DispatchesVerificationEmailWithTheStoredToken()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var emailSender = new TestEmailSender();
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), emailSender);

        await handler.Handle(
            new RegisterCommand("verify-me@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1"), CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Email == "verify-me@shop.test");
        Assert.NotNull(emailSender.LastVerification);
        Assert.Equal(("verify-me@shop.test", "Ama", user.EmailVerificationToken!), emailSender.LastVerification);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsConflict()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), new TestEmailSender());

        await handler.Handle(new RegisterCommand("dupe@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RegisterCommand("dupe@shop.test", "Different1!", "Kofi", "Mensah", null), CancellationToken.None));
    }

    [Fact]
    public async Task Register_EmailIsCaseInsensitive_TreatedAsDuplicate()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), new TestEmailSender());

        await handler.Handle(new RegisterCommand("Case@Shop.Test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RegisterCommand("case@shop.test", "Different1!", "Kofi", "Mensah", null), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("login@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt);
        var result = await loginHandler.Handle(new LoginCommand("login@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        Assert.False(result.RequiresTwoFactor);
        Assert.Equal("login@shop.test", result.Auth!.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsAuthenticationException()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("wrongpw@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            loginHandler.Handle(new LoginCommand("wrongpw@shop.test", "TotallyWrong1!", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsAuthenticationException()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var loginHandler = new LoginCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), _jwt);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            loginHandler.Handle(new LoginCommand("nobody@shop.test", "Passw0rd!", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshToken_NormalSingleRefresh_RotatesTokenAndLinksSuccessor()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("refresh@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var refreshHandler = new RefreshTokenCommandHandler(context, _jwt, tokenIssuer);
        var result = await refreshHandler.Handle(new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);

        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(registerResult.RefreshToken, result.RefreshToken);

        var original = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == _jwt.Hash(registerResult.RefreshToken));
        Assert.True(original.IsRevoked);
        Assert.NotNull(original.ReplacedByTokenId);

        var successor = await context.RefreshTokens.SingleAsync(rt => rt.Id == original.ReplacedByTokenId);
        Assert.True(successor.IsActive);
        Assert.Equal(_jwt.Hash(result.RefreshToken), successor.TokenHash);
    }

    [Fact]
    public async Task RefreshToken_TwoTabsRaceOnSameToken_SecondTabGetsFreshAccessTokenWithoutRevokingChain()
    {
        var setupUser = new TestCurrentUserService();
        var setupContext = _db.CreateContext(setupUser);
        var setupTokenIssuer = new TokenIssuer(setupContext, _jwt);
        var registerResult = await new RegisterCommandHandler(setupContext, _hasher, setupTokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("twotabs@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var contextA = _db.CreateContext(setupUser);
        var contextB = _db.CreateContext(setupUser);

        // Tab A's request reaches the handler first and completes the rotation.
        var resultA = await new RefreshTokenCommandHandler(contextA, _jwt, new TokenIssuer(contextA, _jwt)).Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);
        Assert.NotEmpty(resultA.RefreshToken);

        // Tab B's request was already in flight holding the same (now-rotated) original token.
        var resultB = await new RefreshTokenCommandHandler(contextB, _jwt, new TokenIssuer(contextB, _jwt)).Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);

        Assert.NotEmpty(resultB.AccessToken);
        Assert.Empty(resultB.RefreshToken); // sentinel: AuthController must not touch the cookie for this response

        // No revocation cascade fired, and tab B didn't mint a second RefreshToken row of its own -
        // only the one row from A's rotation is active.
        var activeCount = await setupContext.RefreshTokens
            .CountAsync(rt => rt.UserId == registerResult.User.Id && rt.RevokedAt == null);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task RefreshToken_ReuseOutsideGracePeriod_RevokesEntireChain()
    {
        var setupUser = new TestCurrentUserService();
        var context = _db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("stale-reuse@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var refreshHandler = new RefreshTokenCommandHandler(context, _jwt, tokenIssuer);
        await refreshHandler.Handle(new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);

        // Simulate the grace period having elapsed since rotation - this is genuine reuse now.
        var rotated = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == _jwt.Hash(registerResult.RefreshToken));
        rotated.RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => refreshHandler.Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None));

        var stillActive = await context.RefreshTokens
            .CountAsync(rt => rt.UserId == registerResult.User.Id && rt.RevokedAt == null);
        Assert.Equal(0, stillActive); // whole chain revoked, as before this fix
    }

    [Fact]
    public async Task RefreshToken_ReuseWhenSuccessorAlreadyRevoked_StillRevokesChain()
    {
        var setupUser = new TestCurrentUserService();
        var context = _db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("broken-chain@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var refreshHandler = new RefreshTokenCommandHandler(context, _jwt, tokenIssuer);
        var firstRefresh = await refreshHandler.Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);

        // Successor gets revoked out of band (e.g. an explicit logout) before the reuse attempt.
        var successor = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == _jwt.Hash(firstRefresh.RefreshToken));
        successor.RevokedAt = DateTimeOffset.UtcNow;
        successor.ReasonRevoked = "Logged out";
        await context.SaveChangesAsync(CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => refreshHandler.Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshToken_Expired_ThrowsAuthenticationException()
    {
        var setupUser = new TestCurrentUserService();
        var context = _db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("expired-refresh@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var token = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == _jwt.Hash(registerResult.RefreshToken));
        token.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await context.SaveChangesAsync(CancellationToken.None);

        var refreshHandler = new RefreshTokenCommandHandler(context, _jwt, tokenIssuer);
        await Assert.ThrowsAsync<AuthenticationException>(() => refreshHandler.Handle(
            new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None));
    }

    [Fact]
    public async Task RefreshToken_TwoIndependentSessions_RefreshingOneDoesNotAffectTheOther()
    {
        var setupUser = new TestCurrentUserService();
        var context = _db.CreateContext(setupUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        var registerResult = await new RegisterCommandHandler(context, _hasher, tokenIssuer, new TestEmailSender()).Handle(
            new RegisterCommand("multi-device@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        // A second, independent session - e.g. logging in from another device.
        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt);
        var secondSession = await loginHandler.Handle(
            new LoginCommand("multi-device@shop.test", "Passw0rd!", null, "127.0.0.2"), CancellationToken.None);

        var refreshHandler = new RefreshTokenCommandHandler(context, _jwt, tokenIssuer);
        await refreshHandler.Handle(new RefreshTokenCommand(registerResult.RefreshToken, "127.0.0.1"), CancellationToken.None);

        var secondSessionToken = await context.RefreshTokens
            .SingleAsync(rt => rt.TokenHash == _jwt.Hash(secondSession.Auth!.RefreshToken));
        Assert.True(secondSessionToken.IsActive); // untouched by the other session's rotation
    }

    public void Dispose() => _db.Dispose();
}
