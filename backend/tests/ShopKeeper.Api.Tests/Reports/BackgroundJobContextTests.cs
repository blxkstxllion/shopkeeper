namespace ShopKeeper.Api.Tests.Reports;

using Microsoft.AspNetCore.Http;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Infrastructure.Identity;

/// <summary>Proves CurrentUserService actually falls back to BackgroundJobContext when
/// there's no HttpContext - this is the mechanism ScheduledReportRunner depends on to run
/// GenerateBusinessReportCommand with the right tenant scoping outside a real request.</summary>
public class BackgroundJobContextTests
{
    [Fact]
    public void NoHttpContext_NoPushedJobContext_ResolvesToNull()
    {
        var service = new CurrentUserService(new HttpContextAccessor());

        Assert.Null(service.UserId);
        Assert.Null(service.BusinessId);
        Assert.False(service.IsOwner);
    }

    [Fact]
    public void NoHttpContext_WithPushedJobContext_ResolvesFromIt()
    {
        var service = new CurrentUserService(new HttpContextAccessor());
        var userId = Guid.NewGuid();
        var businessId = Guid.NewGuid();

        using (BackgroundJobContext.Push(new BackgroundJobPrincipal(userId, businessId, null)))
        {
            Assert.Equal(userId, service.UserId);
            Assert.Equal(businessId, service.BusinessId);
            Assert.True(service.IsOwner);
        }

        // Popped on dispose - must not leak into whatever runs next on this async flow.
        Assert.Null(service.BusinessId);
    }
}
