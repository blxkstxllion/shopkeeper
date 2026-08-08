namespace ShopKeeper.Api.Tests.Auth;

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
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt));

        var result = await handler.Handle(
            new RegisterCommand("owner@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1"), CancellationToken.None);

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("owner@shop.test", result.User.Email);
        Assert.Empty(result.User.Businesses); // no business created yet - onboarding comes next
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsConflict()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt));

        await handler.Handle(new RegisterCommand("dupe@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RegisterCommand("dupe@shop.test", "Different1!", "Kofi", "Mensah", null), CancellationToken.None));
    }

    [Fact]
    public async Task Register_EmailIsCaseInsensitive_TreatedAsDuplicate()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var handler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt));

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
        await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("login@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer);
        var result = await loginHandler.Handle(new LoginCommand("login@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        Assert.Equal("login@shop.test", result.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsAuthenticationException()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);
        await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand("wrongpw@shop.test", "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(context, _hasher, tokenIssuer);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            loginHandler.Handle(new LoginCommand("wrongpw@shop.test", "TotallyWrong1!", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsAuthenticationException()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var loginHandler = new LoginCommandHandler(context, _hasher, new TokenIssuer(context, _jwt));

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            loginHandler.Handle(new LoginCommand("nobody@shop.test", "Passw0rd!", null, null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
