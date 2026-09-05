namespace ShopKeeper.Application;

using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(RequireVerifiedEmailBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(RequirePlanTierBehavior<,>));
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));

        services.AddScoped<TokenIssuer>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<PlanLimitService>();
        services.AddScoped<AdvisorCalculations>();

        return services;
    }
}
