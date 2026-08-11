namespace ShopKeeper.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;
using ShopKeeper.Infrastructure.Storage;
using StackExchange.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        // Redis is provisioned in every environment (see docker/docker-compose.yml) but nothing
        // reads from it yet - no feature currently needs caching, sessions, or a queue. Registered
        // as a lazily-connecting singleton, and only when configured, so its absence never breaks
        // startup or any request path. Wire up IDistributedCache/session/queue consumers here
        // if/when a feature actually needs one, rather than adding unused abstractions now.
        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));
        }

        return services;
    }
}
