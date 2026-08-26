namespace ShopKeeper.Application.BusinessSettings.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Deliberately excludes BusinessType, Country, and CurrencyCode - those are foundational
/// choices made at onboarding that historical financial records (sales, prices) already
/// depend on, so this endpoint never lets them silently drift after the fact.
/// </summary>
public record UpdateBusinessProfileCommand(string Name, string? LegalName, string TimeZone, string ColorTheme) : IRequest;

public class UpdateBusinessProfileCommandValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ColorTheme).Must(BusinessColorThemes.All.Contains)
            .WithMessage($"Color theme must be one of: {string.Join(", ", BusinessColorThemes.All)}.");
    }
}

public class UpdateBusinessProfileCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateBusinessProfileCommand>
{
    public async Task Handle(UpdateBusinessProfileCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);
        var businessId = currentUser.RequireBusinessId();

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Business), businessId);

        business.Name = request.Name.Trim();
        business.LegalName = string.IsNullOrWhiteSpace(request.LegalName) ? null : request.LegalName.Trim();
        business.TimeZone = request.TimeZone.Trim();
        business.ColorTheme = request.ColorTheme;

        await db.SaveChangesAsync(cancellationToken);
    }
}
