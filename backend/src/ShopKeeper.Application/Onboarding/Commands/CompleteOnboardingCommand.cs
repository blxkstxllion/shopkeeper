namespace ShopKeeper.Application.Onboarding.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Common.Services;
using ShopKeeper.Application.Onboarding.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record CompleteOnboardingCommand(
    Guid OwnerUserId,
    string BusinessName,
    BusinessType BusinessType,
    string? BusinessTypeOther,
    string Country,
    string CurrencyCode,
    string? LogoUrl,
    bool TaxEnabled,
    decimal TaxRatePercent,
    bool TaxInclusivePricing,
    IReadOnlyList<BusinessGoal> Goals,
    string FirstBranchName,
    string? FirstBranchAddress,
    string? FirstBranchCity,
    string? IpAddress,
    string? UserAgent = null,
    /// <summary>Frontend computes the businessType-based suggestion and whether the user
    /// accepted it (see onboarding.schema.ts) - this handler just validates and stores
    /// whatever comes in, defaulting to the baseline "green" if omitted.</summary>
    string ColorTheme = "green") : IRequest<BusinessDto>;

public class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessTypeOther).NotEmpty().MaximumLength(200)
            .When(x => x.BusinessType == BusinessType.Other)
            .WithMessage("Tell us what kind of business this is.");
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.TaxRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.FirstBranchName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ColorTheme).Must(BusinessColorThemes.All.Contains)
            .WithMessage($"Color theme must be one of: {string.Join(", ", BusinessColorThemes.All)}.");
    }
}

public class CompleteOnboardingCommandHandler(IAppDbContext db, TokenIssuer tokenIssuer)
    : IRequestHandler<CompleteOnboardingCommand, BusinessDto>
{
    public async Task<BusinessDto> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == request.OwnerUserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.OwnerUserId);

        var business = new Business
        {
            Name = request.BusinessName.Trim(),
            BusinessType = request.BusinessType,
            BusinessTypeOther = request.BusinessType == BusinessType.Other ? request.BusinessTypeOther?.Trim() : null,
            Country = request.Country.Trim(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            LogoUrl = request.LogoUrl,
            ColorTheme = request.ColorTheme,
            OnboardingCompleted = true,
            OnboardingStep = 100,
            OnboardingCompletedAt = DateTimeOffset.UtcNow,
        };
        db.Businesses.Add(business);

        db.BusinessSettings.Add(new BusinessSetting
        {
            BusinessId = business.Id,
            Business = business,
            TaxEnabled = request.TaxEnabled,
            TaxRatePercent = request.TaxRatePercent,
            TaxInclusivePricing = request.TaxInclusivePricing,
            Goals = string.Join(',', request.Goals),
        });

        var branch = new Branch
        {
            BusinessId = business.Id,
            Business = business,
            Name = request.FirstBranchName.Trim(),
            Code = "MAIN",
            Address = request.FirstBranchAddress,
            City = request.FirstBranchCity,
            Country = request.Country.Trim(),
            IsMainBranch = true,
        };
        db.Branches.Add(branch);

        var allPermissions = await db.Permissions.ToListAsync(cancellationToken);
        var permissionsByKey = allPermissions.ToDictionary(p => p.Key);

        Role? ownerRole = null;
        foreach (var (roleName, permissionKeys) in DefaultRoles.RolePermissionKeys)
        {
            var role = new Role
            {
                BusinessId = business.Id,
                Business = business,
                Name = roleName,
                IsSystemRole = true,
            };

            foreach (var key in permissionKeys)
            {
                if (permissionsByKey.TryGetValue(key, out var permission))
                {
                    role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
                }
            }

            db.Roles.Add(role);
            if (roleName == DefaultRoles.Owner)
            {
                ownerRole = role;
            }
        }

        if (ownerRole is null)
        {
            throw new InvalidOperationException("Owner role blueprint is missing from DefaultRoles.");
        }

        db.BusinessUsers.Add(new BusinessUser
        {
            BusinessId = business.Id,
            Business = business,
            UserId = owner.Id,
            User = owner,
            Role = ownerRole,
            IsOwner = true,
            Status = BusinessUserStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        var tokens = await tokenIssuer.IssueAsync(owner, business.Id, rememberMe: false, request.IpAddress, request.UserAgent, cancellationToken);

        return new BusinessDto(
            business.Id,
            business.Name,
            business.BusinessType.ToString(),
            business.BusinessTypeOther,
            business.Country,
            business.CurrencyCode,
            business.LogoUrl,
            business.ColorTheme,
            business.OnboardingCompleted,
            branch.Id,
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            tokens.User);
    }
}
