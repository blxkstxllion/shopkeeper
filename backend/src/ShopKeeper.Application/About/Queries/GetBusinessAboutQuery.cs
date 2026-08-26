namespace ShopKeeper.Application.About.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.About.Dtos;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record GetBusinessAboutQuery : IRequest<BusinessAboutDto>;

/// <summary>
/// Description/OwnerBio are visible to every business member - no permission gate, meant to
/// be read by any employee, not just admins. SalesByYear is the one sensitive part (top-line
/// revenue) - populated only if the caller has ReportsView, otherwise returned empty so the
/// page still renders without the achievements section rather than throwing.
/// </summary>
public class GetBusinessAboutQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetBusinessAboutQuery, BusinessAboutDto>
{
    public async Task<BusinessAboutDto> Handle(GetBusinessAboutQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.RequireBusinessId();

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Business), businessId);

        IReadOnlyList<YearlySalesDto> salesByYear = [];
        if (currentUser.HasPermission(PermissionKeys.ReportsView))
        {
            // EF Core's SQLite provider (test suite) can't translate any comparison on
            // Sale.CreatedAt - load first, group in memory. Same workaround already
            // established in GetProfitabilityReportQueryHandler.
            var sales = await db.Sales
                .Where(s => s.Status != SaleStatus.Voided)
                .ToListAsync(cancellationToken);

            salesByYear = sales
                .GroupBy(s => s.CreatedAt.Year)
                .Select(g => new YearlySalesDto(g.Key, g.Sum(s => s.Subtotal - s.DiscountAmount), g.Sum(s => s.GrossProfit), g.Count()))
                .OrderBy(y => y.Year)
                .ToList();
        }

        return new BusinessAboutDto(business.Name, business.LogoUrl, business.Description, business.OwnerBio, salesByYear);
    }
}
