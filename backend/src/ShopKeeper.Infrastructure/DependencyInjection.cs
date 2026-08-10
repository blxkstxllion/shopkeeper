namespace ShopKeeper.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;
using ShopKeeper.Infrastructure.Storage;

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

        return services;
    }
}
