namespace ShopKeeper.Api.Tests.Auth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Infrastructure.Identity;

public class ResendVerificationEmailCommandTests : IDisposable
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
    public async Task Resend_ForUnverifiedUser_IssuesANewTokenAndDispatchesEmail()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var registerHandler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), new TestEmailSender());
        var registerResult = await registerHandler.Handle(
            new RegisterCommand("resend-me@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1"), CancellationToken.None);
        var originalToken = (await context.Users.SingleAsync(u => u.Id == registerResult.User.Id)).EmailVerificationToken;

        var emailSender = new TestEmailSender();
        var handler = new ResendVerificationEmailCommandHandler(context, emailSender);
        await handler.Handle(new ResendVerificationEmailCommand(registerResult.User.Id), CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Id == registerResult.User.Id);
        Assert.NotNull(emailSender.LastVerification);
        Assert.Equal(("resend-me@shop.test", "Ama", user.EmailVerificationToken!), emailSender.LastVerification);
        // A fresh token, not a resend of the same one - the original may already be in an old inbox.
        Assert.NotEqual(originalToken, user.EmailVerificationToken);
    }

    [Fact]
    public async Task Resend_ForAlreadyVerifiedUser_IsANoOp()
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var registerHandler = new RegisterCommandHandler(context, _hasher, new TokenIssuer(context, _jwt), new TestEmailSender());
        var registerResult = await registerHandler.Handle(
            new RegisterCommand("already-verified@shop.test", "Passw0rd!", "Ama", "Owusu", "127.0.0.1"), CancellationToken.None);

        var user = await context.Users.SingleAsync(u => u.Id == registerResult.User.Id);
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        await context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new TestEmailSender();
        var handler = new ResendVerificationEmailCommandHandler(context, emailSender);
        await handler.Handle(new ResendVerificationEmailCommand(registerResult.User.Id), CancellationToken.None);

        Assert.Null(emailSender.LastVerification);
    }

    public void Dispose() => _db.Dispose();
}
