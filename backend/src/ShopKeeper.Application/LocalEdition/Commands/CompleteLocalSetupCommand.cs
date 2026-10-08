namespace ShopKeeper.Application.LocalEdition.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Onboarding.Commands;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

/// <summary>Offline-edition-only. The entire first-run wizard: store name, currency, 4-digit PIN -
/// no email/password/login at all. Internally reuses the exact same CompleteOnboardingCommand the
/// SaaS build's onboarding wizard calls, so the Business/BusinessSetting/Branch/Roles/BusinessUser
/// graph it produces (and every handler downstream that assumes that graph exists) needs zero
/// changes for this edition.</summary>
public record CompleteLocalSetupCommand(string StoreName, string CurrencyCode, string Pin) : IRequest;

public class CompleteLocalSetupCommandValidator : AbstractValidator<CompleteLocalSetupCommand>
{
    public CompleteLocalSetupCommandValidator()
    {
        RuleFor(x => x.StoreName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Pin).Matches("^[0-9]{4}$").WithMessage("PIN must be exactly 4 digits.");
    }
}

public class CompleteLocalSetupCommandHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    ISender sender,
    ILocalIdentityCache identityCache) : IRequestHandler<CompleteLocalSetupCommand>
{
    /// <summary>CompleteOnboardingCommand requires a Country alongside CurrencyCode and, like
    /// CurrencyCode itself, locks it in permanently (see UpdateBusinessProfileCommand's doc
    /// comment) - but the offline wizard deliberately only asks for currency, so this maps each
    /// supported currency to a representative home country. Purely cosmetic (shows up on
    /// receipts/reports headers) - nothing financial depends on Country the way it depends on
    /// CurrencyCode, so an approximate mapping here is safe.</summary>
    private static readonly Dictionary<string, string> CountryByCurrency = new()
    {
        ["GHS"] = "Ghana",
        ["NGN"] = "Nigeria",
        ["KES"] = "Kenya",
        ["ZAR"] = "South Africa",
        ["UGX"] = "Uganda",
        ["TZS"] = "Tanzania",
        ["RWF"] = "Rwanda",
        ["ZMW"] = "Zambia",
        ["MWK"] = "Malawi",
        ["BWP"] = "Botswana",
        ["NAD"] = "Namibia",
        ["MZN"] = "Mozambique",
        ["ETB"] = "Ethiopia",
        ["EGP"] = "Egypt",
        ["MAD"] = "Morocco",
        ["XOF"] = "Ivory Coast",
        ["XAF"] = "Cameroon",
        ["CDF"] = "DR Congo",
        ["SLL"] = "Sierra Leone",
        ["LRD"] = "Liberia",
        ["AOA"] = "Angola",
        ["MUR"] = "Mauritius",
        ["GBP"] = "United Kingdom",
        ["USD"] = "United States",
        ["EUR"] = "France",
        ["CAD"] = "Canada",
        ["AED"] = "United Arab Emirates",
        ["INR"] = "India",
        ["CNY"] = "China",
        ["AUD"] = "Australia",
    };

    public async Task Handle(CompleteLocalSetupCommand request, CancellationToken cancellationToken)
    {
        var alreadySetUp = await db.LocalAppSettings.AnyAsync(cancellationToken);
        if (alreadySetUp)
        {
            throw new ConflictException("Setup has already been completed.");
        }

        var currencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        var country = CountryByCurrency.GetValueOrDefault(currencyCode, "Not specified");
        var storeName = request.StoreName.Trim();

        // No email/password concept in this edition - a fixed sentinel email and a random,
        // never-surfaced password hash just satisfy User's NOT NULL columns. IsEmailVerified=true
        // is load-bearing: RequireVerifiedEmailBehavior's exemption list only covers Auth/Onboarding
        // namespaces, so without this every subsequent request would be blocked.
        var owner = new User
        {
            Email = "owner@local.shopkeeper",
            PasswordHash = hasher.Hash(Guid.NewGuid().ToString("N")),
            FirstName = "Store",
            LastName = "Owner",
            IsEmailVerified = true,
            EmailVerificationEnforced = false,
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync(cancellationToken);

        var business = await sender.Send(
            new CompleteOnboardingCommand(
                OwnerUserId: owner.Id,
                BusinessName: storeName,
                BusinessType: BusinessType.Retail,
                BusinessTypeOther: null,
                Country: country,
                CurrencyCode: currencyCode,
                LogoUrl: null,
                TaxEnabled: false,
                TaxRatePercent: 0,
                TaxInclusivePricing: true,
                Goals: Array.Empty<BusinessGoal>(),
                FirstBranchName: "Main",
                FirstBranchAddress: null,
                FirstBranchCity: null,
                IpAddress: null),
            cancellationToken);

        // EnterpriseAi unlocks Reports/CustomRoles/unlimited branches (PlanLimits.For) with zero
        // changes to RequirePlanTierBehavior. HasAi stays harmless: UnavailableAdvisorConversationClient
        // still reports IsConfigured=false with no Anthropic key, and this edition's frontend never
        // bundles the AI Advisor route at all.
        var businessEntity = await db.Businesses.FirstAsync(b => b.Id == business.Id, cancellationToken);
        businessEntity.PlanTier = PlanTier.EnterpriseAi;

        db.LocalAppSettings.Add(new LocalAppSettings
        {
            StoreName = storeName,
            PinHash = hasher.Hash(request.Pin),
            SetupCompletedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        await identityCache.RefreshAsync(cancellationToken);
    }
}
