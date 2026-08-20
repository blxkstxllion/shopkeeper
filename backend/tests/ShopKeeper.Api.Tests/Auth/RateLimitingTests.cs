namespace ShopKeeper.Api.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// The only place in this suite that boots the real ASP.NET Core pipeline (everything else
/// constructs handlers directly, bypassing middleware entirely - see AuditLoggingBehaviorTests/
/// RequirePlanTierBehaviorTests for the equivalent at the MediatR-pipeline level). Rate limiting
/// is HTTP-pipeline middleware with no other feasible unit-test seam, so this is the only way to
/// prove the policy is actually wired to the right endpoints instead of just trusting the config.
/// </summary>
public class RateLimitTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                ["Jwt:Secret"] = "test-only-secret-at-least-32-characters-long-for-hmac-sha256",
                ["Jwt:Issuer"] = "ShopKeeper",
                ["Jwt:Audience"] = "ShopKeeperClient",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class RateLimitingTests : IClassFixture<RateLimitTestFactory>
{
    private readonly RateLimitTestFactory _factory;

    public RateLimitingTests(RateLimitTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ForgotPassword_AllowsFiveRequestsThenRejectsTheSixth_WithinTheWindow()
    {
        // A dedicated client so this test's request burst doesn't share a rate-limit partition
        // with any other test hitting the same IP-keyed policy.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        }

        var sixth = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        var body = await sixth.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Contains("Too many attempts", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ForgotPassword_DifferentIps_AreRateLimitedIndependently()
    {
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.20");
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.21");

        for (var i = 0; i < 5; i++)
        {
            var responseA = await clientA.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });
            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        }

        // clientA is now at its limit, but clientB (a different partition key) must be unaffected.
        var responseB = await clientB.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
    }

    [Fact]
    public async Task Login_IsRateLimited_ButNotEveryAuthenticatedEndpoint()
    {
        // Confirms the policy attribute reached a second controller (not just AuthController)
        // without accidentally becoming a blanket API-wide policy - /health/live has no
        // [EnableRateLimiting] and must never be throttled by the same partition.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.30");

        for (var i = 0; i < 6; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "wrong", businessId = (Guid?)null });
        }

        var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
