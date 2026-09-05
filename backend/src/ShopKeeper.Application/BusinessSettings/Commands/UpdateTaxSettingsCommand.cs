namespace ShopKeeper.Application.BusinessSettings.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateTaxSettingsCommand(
    bool TaxEnabled, string? TaxIdNumber, decimal TaxRatePercent, bool TaxInclusivePricing, Guid? ClientRequestId = null)
    : IRequest, ISupportsClientRequestId;

public class UpdateTaxSettingsCommandValidator : AbstractValidator<UpdateTaxSettingsCommand>
{
    public UpdateTaxSettingsCommandValidator()
    {
        RuleFor(x => x.TaxIdNumber).MaximumLength(100);
        RuleFor(x => x.TaxRatePercent).InclusiveBetween(0, 100);
    }
}

public class UpdateTaxSettingsCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateTaxSettingsCommand>
{
    public async Task Handle(UpdateTaxSettingsCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);
        var businessId = currentUser.RequireBusinessId();

        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), businessId);

        setting.TaxEnabled = request.TaxEnabled;
        setting.TaxIdNumber = string.IsNullOrWhiteSpace(request.TaxIdNumber) ? null : request.TaxIdNumber.Trim();
        setting.TaxRatePercent = request.TaxRatePercent;
        setting.TaxInclusivePricing = request.TaxInclusivePricing;

        await db.SaveChangesAsync(cancellationToken);
    }
}
