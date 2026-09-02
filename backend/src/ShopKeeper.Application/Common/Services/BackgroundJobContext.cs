namespace ShopKeeper.Application.Common.Services;

/// <summary>
/// Ambient tenant/user context for code that runs outside a normal HTTP request - currently
/// only ScheduledReportRunner - where there's no JWT/HttpContext for CurrentUserService to
/// read claims from. Push it around a mediator.Send(...) call inside a job; Infrastructure's
/// CurrentUserService falls back to this when HttpContext is null. AsyncLocal so it correctly
/// flows through the async handler chain without leaking between concurrently-running jobs.
/// </summary>
public static class BackgroundJobContext
{
    private static readonly AsyncLocal<BackgroundJobPrincipal?> Current = new();

    public static BackgroundJobPrincipal? CurrentPrincipal => Current.Value;

    public static IDisposable Push(BackgroundJobPrincipal principal)
    {
        Current.Value = principal;
        return new Popper();
    }

    private sealed class Popper : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }
}

/// <summary>Owner-equivalent by design: a scheduled report wouldn't have been created without
/// adequate permission in the first place, so the run executes with full access rather than
/// re-checking the creating user's current permissions on every tick.</summary>
public record BackgroundJobPrincipal(Guid UserId, Guid BusinessId, Guid? BranchId);
