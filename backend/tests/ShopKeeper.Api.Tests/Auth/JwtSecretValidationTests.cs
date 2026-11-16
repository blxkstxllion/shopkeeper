namespace ShopKeeper.Api.Tests.Auth;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Regression test for a real gap: a short-but-nonempty Jwt:Secret used to boot the app fine
/// and silently sign every token with a brute-forceable key (an empty secret already crashed
/// on its own via SymmetricSecurityKey's "key length is zero", but 1-31 bytes did not). Proves
/// the ValidateOnStart() check added to DependencyInjection.AddInfrastructure actually fires
/// during host startup, not just that the validation rule exists.
/// </summary>
public class JwtSecretValidationTests
{
    [Fact]
    public void Startup_WithShortNonEmptyJwtSecret_FailsFastWithAnActionableMessage()
    {
        using var factory = new WeakSecretTestFactory();

        var ex = Record.Exception(() => factory.Services);

        Assert.NotNull(ex);
        Assert.Contains("Jwt:Secret must be at least 32 bytes", ex.ToString());
    }

    [Fact]
    public void Startup_WithThirtyTwoByteJwtSecret_BootsSuccessfully()
    {
        using var factory = new ValidSecretTestFactory();

        var ex = Record.Exception(() => factory.Services);

        Assert.Null(ex);
    }

    private class WeakSecretTestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "DataSource=:memory:",
                    ["Jwt:Secret"] = "short", // 5 bytes - well under the 32-byte minimum
                    ["Jwt:Issuer"] = "ShopKeeper",
                    ["Jwt:Audience"] = "ShopKeeperClient",
                });
            });
        }
    }

    private class ValidSecretTestFactory : WebApplicationFactory<Program>
    {
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
                });
            });
        }
    }
}
