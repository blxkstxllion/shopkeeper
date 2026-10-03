namespace ShopKeeper.Api.Tests.Plans;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Payments;
using ShopKeeper.Infrastructure.Persistence;

/// <summary>
/// Mirrors RateLimitingTests's WebApplicationFactory pattern - the webhook controller reads the
/// raw request body before model binding and checks a real header, so (like rate limiting) this
/// is the one part of this feature that genuinely needs the real ASP.NET Core pipeline rather
/// than calling a handler directly.
///
/// Uses a named shared-cache in-memory database (one SqliteConnection *string*, not one shared
/// connection *object*) - same reasoning as ConcurrentSqliteTestDatabase. A single
/// SqliteConnection object can't safely run two commands at once, so
/// Receive_ConcurrentIdenticalDeliveries below (which fires two genuinely concurrent requests)
/// was flaky: ASP.NET Core creates a separate scoped AppDbContext per request as normal, but
/// both contexts were sharing the one underlying connection object, so a real race could throw
/// a connection-contention exception that isn't a DbUpdateException - not caught by the
/// handler's race-recovery logic, surfacing as an unhandled 500 instead of exercising the
/// actual race path under test. A connection string instead lets EF Core open an independent
/// connection per DbContext onto the same named database, so concurrent requests race at the
/// SQLite engine level (the thing actually being tested) instead of colliding on ADO.NET
/// connection state (an artifact of the old test setup, nothing to do with the real race).
/// </summary>
public class PaystackWebhookTestFactory : WebApplicationFactory<Program>
{
    public const string TestSecretKey = "sk_test_webhook_pipeline_secret";

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"file:{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared,
        DefaultTimeout = 5,
    }.ToString();

    private readonly SqliteConnection _anchor;

    public PaystackWebhookTestFactory()
    {
        // Keeps the named shared-cache database alive for the factory's lifetime - it's
        // destroyed the instant its last connection closes, and every other connection is
        // opened-and-closed per request.
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                ["Jwt:Secret"] = "test-only-secret-at-least-32-characters-long-for-hmac-sha256",
                ["Jwt:Issuer"] = "ShopKeeper",
                ["Jwt:Audience"] = "ShopKeeperClient",
                ["Paystack:SecretKey"] = TestSecretKey,
                ["Paystack:BusinessPlanCode"] = "PLN_test_business",
                ["Paystack:FrontendBaseUrl"] = "https://app.test",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connectionString));

            // Overriding the DI registration directly (like the DbContext above) rather than
            // relying on Paystack:SecretKey reaching DependencyInjection.AddInfrastructure's own
            // (eager, pre-Build()) config read - that read happens inside Program.cs's top-level
            // statements, before this factory's ConfigureAppConfiguration additions are
            // guaranteed to have taken effect. None of these tests call a PaystackClient method
            // that makes a real HTTP call (only VerifyWebhookSignature, which is pure computation),
            // so a real client wired to a never-invoked HttpClient is safe here.
            services.RemoveAll<IPaystackClient>();
            services.AddSingleton<IPaystackClient>(new PaystackClient(
                new HttpClient { BaseAddress = new Uri("https://api.paystack.co/") },
                Options.Create(new PaystackSettings { SecretKey = TestSecretKey }),
                NullLogger<PaystackClient>.Instance));
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
        if (disposing) _anchor.Dispose();
    }
}

public class PaystackWebhookControllerTests : IClassFixture<PaystackWebhookTestFactory>
{
    private readonly PaystackWebhookTestFactory _factory;

    public PaystackWebhookControllerTests(PaystackWebhookTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Receive_ValidSignature_ReturnsOkAndRecordsEvent()
    {
        var client = _factory.CreateClient();
        // Unique event type per test (not a real Paystack event name) so this test's row is
        // unambiguously identifiable regardless of what other tests in this IClassFixture-shared
        // database have already written.
        var rawBody = """{"event":"test.valid_signature","data":{"reference":"chk_pipeline_test"}}""";

        var response = await PostWebhookAsync(client, rawBody, Sign(rawBody, PaystackWebhookTestFactory.TestSecretKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PaystackWebhookEvents.CountAsync(e => e.EventType == "test.valid_signature"));
    }

    [Fact]
    public async Task Receive_WrongSignature_ReturnsUnauthorized_AndDoesNotRecordEvent()
    {
        var client = _factory.CreateClient();
        var rawBody = """{"event":"test.wrong_signature","data":{"reference":"chk_pipeline_wrong_sig"}}""";

        var response = await PostWebhookAsync(client, rawBody, "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.PaystackWebhookEvents.CountAsync(e => e.EventType == "test.wrong_signature"));
    }

    [Fact]
    public async Task Receive_ConcurrentIdenticalDeliveries_BothSucceed_OnlyOneEventRecorded()
    {
        // Regression test for the check-then-act race in ProcessPaystackWebhookCommandHandler:
        // two genuinely concurrent identical retries both pass the AnyAsync precheck before
        // either has inserted, so the unique index on RawPayloadHash decides the winner and the
        // loser's SaveChangesAsync throws DbUpdateException. Before the fix, that exception was
        // unhandled and surfaced as a 500 (Paystack would then retry into the same no-op again -
        // self-healing, but wrong). Fires two real concurrent HTTP requests through the actual
        // pipeline (shared SQLite connection, separate per-request DbContext) so this either
        // reproduces the real race or it doesn't - no mocking the concurrency away.
        var client = _factory.CreateClient();
        var rawBody = """{"event":"test.concurrent_delivery","data":{"reference":"chk_pipeline_concurrent"}}""";
        var signature = Sign(rawBody, PaystackWebhookTestFactory.TestSecretKey);

        var responses = await Task.WhenAll(
            PostWebhookAsync(client, rawBody, signature),
            PostWebhookAsync(client, rawBody, signature));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PaystackWebhookEvents.CountAsync(e => e.EventType == "test.concurrent_delivery"));
    }

    [Fact]
    public async Task Receive_MissingSignatureHeader_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var rawBody = """{"event":"charge.success","data":{"reference":"chk_pipeline_no_header"}}""";

        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/webhooks/paystack", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string rawBody, string signature)
    {
        using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paystack") { Content = content };
        request.Headers.TryAddWithoutValidation("x-paystack-signature", signature);
        return await client.SendAsync(request);
    }

    private static string Sign(string rawBody, string secretKey) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
}
