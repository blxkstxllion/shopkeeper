namespace ShopKeeper.Api.Tests.Auth;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Infrastructure.Identity;

public class TwoFactorCommandTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly TotpService _totp = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private static string ComputeValidCode(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private async Task<(Domain.Entities.User User, TestCurrentUserService CurrentUser, Infrastructure.Persistence.AppDbContext Context, TokenIssuer TokenIssuer)>
        RegisterUserAsync(string email)
    {
        var currentUser = new TestCurrentUserService();
        var context = _db.CreateContext(currentUser);
        var tokenIssuer = new TokenIssuer(context, _jwt);

        var result = await new RegisterCommandHandler(context, _hasher, tokenIssuer).Handle(
            new RegisterCommand(email, "Passw0rd!", "Ama", "Owusu", null), CancellationToken.None);
        currentUser.UserId = result.User.Id;

        var user = await context.Users.SingleAsync(u => u.Id == result.User.Id);
        return (user, currentUser, context, tokenIssuer);
    }

    [Fact]
    public async Task Setup_GeneratesSecretWithoutEnabling()
    {
        var (user, currentUser, context, _) = await RegisterUserAsync("2fa-setup@shop.test");

        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);

        Assert.NotEmpty(setup.Secret);
        Assert.Contains("otpauth://totp/", setup.ProvisioningUri);

        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.False(stored.TwoFactorEnabled);
        Assert.Equal(setup.Secret, stored.TwoFactorSecret);
    }

    [Fact]
    public async Task Enable_WithValidCode_EnablesAndReturnsRecoveryCodes()
    {
        var (user, _, context, _) = await RegisterUserAsync("2fa-enable@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);

        var recoveryCodes = await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        Assert.Equal(8, recoveryCodes.Count);
        Assert.Equal(8, recoveryCodes.Distinct().Count());

        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.True(stored.TwoFactorEnabled);
        Assert.NotNull(stored.TwoFactorRecoveryCodesJson);
    }

    [Fact]
    public async Task Enable_WithInvalidCode_ThrowsAuthenticationException()
    {
        var (user, _, context, _) = await RegisterUserAsync("2fa-badcode@shop.test");
        await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, "000000"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithTwoFactorEnabled_DoesNotIssueTokensAndReturnsChallenge()
    {
        var (user, currentUser, context, tokenIssuer) = await RegisterUserAsync("2fa-login@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        var loginResult = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("2fa-login@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        Assert.True(loginResult.RequiresTwoFactor);
        Assert.NotEmpty(loginResult.ChallengeToken!);
        Assert.Null(loginResult.Auth);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithValidTotpCode_CompletesLoginAndIssuesTokens()
    {
        var (user, _, context, tokenIssuer) = await RegisterUserAsync("2fa-verify@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        var loginResult = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("2fa-verify@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        var verifyHandler = new VerifyTwoFactorCommandHandler(context, _jwt, _totp, _hasher, tokenIssuer);
        var auth = await verifyHandler.Handle(
            new VerifyTwoFactorCommand(loginResult.ChallengeToken!, ComputeValidCode(setup.Secret), null), CancellationToken.None);

        Assert.NotEmpty(auth.AccessToken);
        Assert.Equal("2fa-verify@shop.test", auth.User.Email);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithWrongCode_ThrowsAuthenticationException()
    {
        var (user, _, context, tokenIssuer) = await RegisterUserAsync("2fa-wrongcode@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        var loginResult = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("2fa-wrongcode@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        var verifyHandler = new VerifyTwoFactorCommandHandler(context, _jwt, _totp, _hasher, tokenIssuer);

        await Assert.ThrowsAsync<AuthenticationException>(() => verifyHandler.Handle(
            new VerifyTwoFactorCommand(loginResult.ChallengeToken!, "000000", null), CancellationToken.None));
    }

    [Fact]
    public async Task VerifyTwoFactor_WithRecoveryCode_CompletesLoginAndConsumesCodeOnce()
    {
        var (user, _, context, tokenIssuer) = await RegisterUserAsync("2fa-recovery@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        var recoveryCodes = await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);
        var recoveryCode = recoveryCodes[0];

        var loginResult = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("2fa-recovery@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        var verifyHandler = new VerifyTwoFactorCommandHandler(context, _jwt, _totp, _hasher, tokenIssuer);
        var auth = await verifyHandler.Handle(
            new VerifyTwoFactorCommand(loginResult.ChallengeToken!, recoveryCode, null), CancellationToken.None);
        Assert.NotEmpty(auth.AccessToken);

        // The same recovery code must not work a second time.
        var secondLoginResult = await new LoginCommandHandler(context, _hasher, tokenIssuer, _jwt).Handle(
            new LoginCommand("2fa-recovery@shop.test", "Passw0rd!", null, null), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => verifyHandler.Handle(
            new VerifyTwoFactorCommand(secondLoginResult.ChallengeToken!, recoveryCode, null), CancellationToken.None));
    }

    [Fact]
    public async Task Disable_WithCorrectPassword_TurnsOffTwoFactor()
    {
        var (user, _, context, _) = await RegisterUserAsync("2fa-disable@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        await new DisableTwoFactorCommandHandler(context, _hasher).Handle(
            new DisableTwoFactorCommand(user.Id, "Passw0rd!"), CancellationToken.None);

        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.False(stored.TwoFactorEnabled);
        Assert.Null(stored.TwoFactorSecret);
        Assert.Null(stored.TwoFactorRecoveryCodesJson);
    }

    [Fact]
    public async Task Disable_WithWrongPassword_ThrowsAuthenticationException()
    {
        var (user, _, context, _) = await RegisterUserAsync("2fa-disable-wrong@shop.test");
        var setup = await new SetupTwoFactorCommandHandler(context, _totp).Handle(new SetupTwoFactorCommand(user.Id), CancellationToken.None);
        await new EnableTwoFactorCommandHandler(context, _totp, _hasher).Handle(
            new EnableTwoFactorCommand(user.Id, ComputeValidCode(setup.Secret)), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => new DisableTwoFactorCommandHandler(context, _hasher).Handle(
            new DisableTwoFactorCommand(user.Id, "WrongPassword1!"), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
