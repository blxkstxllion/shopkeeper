namespace ShopKeeper.Api.Tests.Common;

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Auth.Commands;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Ai;
using ShopKeeper.Infrastructure.Identity;

/// <summary>
/// Regression coverage for a real bug found while building an unrelated feature: every pipeline
/// behavior (Validation, AuditLogging, RequirePlanTier) silently never ran for a plain (void)
/// IRequest command - see the comment on ValidationBehavior for the root cause. Every existing
/// pipeline-behavior test in this suite (AuditLoggingBehaviorTests, RequirePlanTierBehaviorTests)
/// happens to use a typed IRequest{T} command, which is exactly why this went uncaught - these
/// tests specifically use a void command (ChangePasswordCommand) through a real MediatR pipeline,
/// the only way this behavior actually runs at all.
/// </summary>
public class ValidationBehaviorTests : IDisposable
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
        services.AddSingleton<IPasswordHasher>(_hasher);
        services.AddSingleton<IAdvisorNarrator>(new PassthroughAdvisorNarrator());
        services.AddSingleton<IAdvisorConversationClient>(new UnavailableAdvisorConversationClient());
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task VoidCommand_ThroughRealPipeline_IsActuallyValidated()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        // 2-character new password violates ChangePasswordCommandValidator's MinimumLength(8) -
        // before the fix, this reached the handler anyway (throwing AuthenticationException from
        // a wrong-current-password check instead), proving validation never ran at all.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            sender.Send(new ChangePasswordCommand(owner.UserId!.Value, "whatever", "ab"), CancellationToken.None));
    }

    [Fact]
    public async Task TypedCommand_ThroughRealPipeline_WasAlreadyValidated_UnaffectedByTheFix()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        // RegisterCommand returns AuthResultDto (not void) - this path always worked; asserting
        // it explicitly so a future regression in the other direction would also be caught.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            sender.Send(new RegisterCommand("not-an-email", "short", "Ama", "Owusu", null), CancellationToken.None));
    }

    public void Dispose() => _db.Dispose();
}
