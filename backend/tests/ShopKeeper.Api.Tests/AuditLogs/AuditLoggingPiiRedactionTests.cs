namespace ShopKeeper.Api.Tests.AuditLogs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopKeeper.Api.Tests.TestHelpers;
using ShopKeeper.Application;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Commands;
using ShopKeeper.Application.Products.Commands;
using ShopKeeper.Application.Suppliers.Commands;
using ShopKeeper.Infrastructure.Identity;
using ShopKeeper.Infrastructure.Persistence;
using MediatR;

/// <summary>
/// Same "goes through the real MediatR pipeline" rationale as AuditLoggingBehaviorTests - a
/// pipeline behavior can only be exercised end-to-end, not by calling a handler directly.
/// </summary>
public class AuditLoggingPiiRedactionTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly JwtTokenService _jwt = new(Options.Create(PosTestFixture.JwtTestSettings));

    private ISender BuildSender(IAppDbContext context, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IAppDbContext>(context);
        services.AddSingleton(currentUser);
        services.AddSingleton<IPasswordHasher>(_hasher);
        services.AddSingleton<IJwtTokenService>(_jwt);
        services.AddSingleton<IEmailSender>(new TestEmailSender());
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task CreateCustomerCommand_ThroughPipeline_RedactsNamePhoneEmailAddress()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        await sender.Send(
            new CreateCustomerCommand("Ama Serwaa", "0244000000", "ama@customer.test", "12 High St"), CancellationToken.None);

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Action == "CreateCustomer");
        Assert.DoesNotContain("Ama Serwaa", log.NewValue);
        Assert.DoesNotContain("0244000000", log.NewValue);
        Assert.DoesNotContain("ama@customer.test", log.NewValue);
        Assert.DoesNotContain("12 High St", log.NewValue);
        Assert.Contains("[REDACTED]", log.NewValue);
    }

    [Fact]
    public async Task CreateSupplierCommand_ThroughPipeline_RedactsNameContactNamePhoneEmailAddress()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        await sender.Send(
            new CreateSupplierCommand("Accra Wholesale Ltd", "Kwame Mensah", "0244111111", "kwame@supplier.test", "5 Market Rd"),
            CancellationToken.None);

        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Action == "CreateSupplier");
        Assert.DoesNotContain("Accra Wholesale Ltd", log.NewValue);
        Assert.DoesNotContain("Kwame Mensah", log.NewValue);
        Assert.DoesNotContain("0244111111", log.NewValue);
        Assert.DoesNotContain("kwame@supplier.test", log.NewValue);
        Assert.DoesNotContain("5 Market Rd", log.NewValue);
        Assert.Contains("[REDACTED]", log.NewValue);
    }

    [Fact]
    public async Task CreateProductCommand_ThroughPipeline_DoesNotRedactName()
    {
        var seeded = await PosTestFixture.SeedAsync(_db, _hasher, _jwt);
        var owner = seeded.AsOwner();
        var context = _db.CreateContext(owner);
        var sender = BuildSender(context, owner);

        await sender.Send(
            new CreateProductCommand("Widget", "SKU-PII-1", null, null, null, null, 10m, 6m, 0, true, 5, seeded.BranchId),
            CancellationToken.None);

        // Proves the fix is scoped to the [SensitiveData] attribute on specific Customer/Supplier
        // properties, not an accidental blanket "any property named Name gets redacted" rule.
        var log = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Action == "CreateProduct");
        Assert.Contains("Widget", log.NewValue);
        Assert.DoesNotContain("[REDACTED]", log.NewValue);
    }

    public void Dispose() => _db.Dispose();
}
