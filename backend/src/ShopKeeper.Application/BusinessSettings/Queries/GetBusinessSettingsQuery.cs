namespace ShopKeeper.Application.BusinessSettings.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.BusinessSettings.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record GetBusinessSettingsQuery : IRequest<BusinessSettingsDto>;

public class GetBusinessSettingsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetBusinessSettingsQuery, BusinessSettingsDto>
{
    public async Task<BusinessSettingsDto> Handle(GetBusinessSettingsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SettingsManage);
        var businessId = currentUser.RequireBusinessId();

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Business), businessId);
        var setting = await db.BusinessSettings.FirstOrDefaultAsync(s => s.BusinessId == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessSetting), businessId);

        return new BusinessSettingsDto(
            business.Id, business.Name, business.LegalName, business.BusinessType.ToString(), business.Country,
            business.CurrencyCode, business.TimeZone, business.LogoUrl,
            setting.TaxEnabled, setting.TaxIdNumber, setting.TaxRatePercent, setting.TaxInclusivePricing);
    }
}
