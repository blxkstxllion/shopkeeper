namespace ShopKeeper.Infrastructure;

using Amazon;
using Amazon.SimpleEmailV2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Payments;
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
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        // Real delivery (SES) only when an Email:FromAddress is configured - same "absence
        // never breaks startup" pattern as Redis below. Local/CI dev has none configured, so
        // it keeps using LoggingEmailSender; production must set Email:FromAddress (and
        // Email:FrontendBaseUrl) for real mail to go out at all.
        var emailFromAddress = configuration["Email:FromAddress"];
        if (!string.IsNullOrWhiteSpace(emailFromAddress))
        {
            services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
            var region = configuration["Email:Region"] ?? "us-east-1";
            services.AddSingleton<IAmazonSimpleEmailServiceV2>(
                _ => new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(region)));
            services.AddScoped<IEmailSender, SesEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        // Real Paystack calls only when Paystack:SecretKey is configured - same "absence never
        // breaks startup" pattern as Email above. Local/CI dev has none configured by default, so
        // SetPlanTierCommand stays fully self-serve/payment-free exactly as it is today; setting
        // this switches it to the restricted, checkout-based flow.
        var paystackSecretKey = configuration["Paystack:SecretKey"];
        if (!string.IsNullOrWhiteSpace(paystackSecretKey))
        {
            services.Configure<PaystackSettings>(configuration.GetSection(PaystackSettings.SectionName));
            services.AddHttpClient<IPaystackClient, PaystackClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.paystack.co/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", paystackSecretKey);
            });
        }
        else
        {
            services.AddScoped<IPaystackClient, DevPaystackClient>();
        }

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
