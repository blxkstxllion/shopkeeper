namespace ShopKeeper.Infrastructure.Identity;

using ShopKeeper.Application.Common.Interfaces;

/// <summary>
/// Registered unconditionally by AddInfrastructure, same "absence never breaks startup" pattern
/// as Email/Paystack/Anthropic - MediatR's assembly scan registers every Application-layer
/// handler (including CompleteLocalSetupCommandHandler, which depends on ILocalIdentityCache) in
/// every host that references ShopKeeper.Application, not just ShopKeeper.Api.Local. Without this
/// fallback, the SaaS build's DI container fails to validate since nothing else in it ever
/// registers ILocalIdentityCache. Genuinely never exercised there in practice - no SaaS controller
/// exposes the LocalEdition commands/queries this cache backs. ShopKeeper.Api.Local's own
/// Program.cs registers the real LocalIdentityCache after AddInfrastructure runs, which wins as
/// the last registration for the same service type.
/// </summary>
public class NoOpLocalIdentityCache : ILocalIdentityCache
{
    public LocalIdentitySnapshot Current => LocalIdentitySnapshot.Empty;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
