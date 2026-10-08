namespace ShopKeeper.Api.Local;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>The offline edition's only auth scheme - authenticates every request as the single
/// local owner, regardless of PIN-lock state (see UnlockLocalAppCommand's doc comment: the PIN is
/// a UI gate, not an API authorization boundary - there's no "logged out" state for this API, only
/// for the frontend UI). Stamps the same claim names JwtTokenService uses in the SaaS build
/// ("sub"/"business_id"/"branch_id"/"is_owner"), so CurrentUserService, the tenant query filter,
/// and RequirePlanTierBehavior/RequireVerifiedEmailBehavior all work completely unchanged. Before
/// setup has run, ILocalIdentityCache.Current is LocalIdentitySnapshot.Empty, so every claim is
/// simply absent - any [Authorize]'d business endpoint correctly fails RequireBusinessId() etc.,
/// while the unauthenticated LocalController endpoints (status/setup/unlock) still work.</summary>
public class LocalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ILocalIdentityCache identityCache)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Local";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = identityCache.Current;

        var claims = new List<Claim> { new("is_owner", identity.IsOwner ? "true" : "false") };
        if (identity.UserId is { } userId) claims.Add(new Claim("sub", userId.ToString()));
        if (identity.BusinessId is { } businessId) claims.Add(new Claim("business_id", businessId.ToString()));
        if (identity.BranchId is { } branchId) claims.Add(new Claim("branch_id", branchId.ToString()));

        var claimsIdentity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
