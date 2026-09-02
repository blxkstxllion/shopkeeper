namespace ShopKeeper.Application.Reports.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Reports.Dtos;
using ShopKeeper.Domain.Constants;

public record GetScheduledReportsQuery : IRequest<IReadOnlyList<ScheduledReportDto>>, IRequirePlanFeature
{
    public bool RequiresReports => true;
    public bool RequiresAi => false;
    public bool RequiresCustomRoles => false;
}

public class GetScheduledReportsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetScheduledReportsQuery, IReadOnlyList<ScheduledReportDto>>
{
    public async Task<IReadOnlyList<ScheduledReportDto>> Handle(GetScheduledReportsQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ReportsView);
        var businessId = currentUser.RequireBusinessId();

        // OrderBy is client-side (after ToListAsync), not translated into the query - the
        // SQLite test provider can't translate ORDER BY on a DateTimeOffset column, the same
        // limitation GetProfitabilityReportQuery already works around for date-range filtering.
        var reports = await db.ScheduledReports
            .Where(r => r.BusinessId == businessId)
            .Select(r => new
            {
                r.Id,
                r.BranchId,
                BranchName = r.Branch != null ? r.Branch.Name : null,
                r.Frequency,
                r.Format,
                r.RecipientEmails,
                r.IsActive,
                r.NextRunAt,
                r.LastRunAt,
            })
            .ToListAsync(cancellationToken);

        return reports
            .OrderBy(r => r.NextRunAt)
            .Select(r => new ScheduledReportDto(
                r.Id, r.BranchId, r.BranchName, r.Frequency, r.Format,
                r.RecipientEmails.Split(',', StringSplitOptions.RemoveEmptyEntries), r.IsActive, r.NextRunAt, r.LastRunAt))
            .ToList();
    }
}
