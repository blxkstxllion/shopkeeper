namespace ShopKeeper.Application.Customers.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Domain.Constants;

public record GetCustomerLedgerQuery(Guid CustomerId, int Page, int PageSize) : IRequest<PagedResult<CustomerLedgerEntryDto>>;

public class GetCustomerLedgerQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCustomerLedgerQuery, PagedResult<CustomerLedgerEntryDto>>
{
    public async Task<PagedResult<CustomerLedgerEntryDto>> Handle(GetCustomerLedgerQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);

        var query = db.CustomerLedgerEntries.Where(e => e.CustomerId == request.CustomerId);
        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        // Ordered client-side, not via OrderByDescending in the query - the SQLite test
        // provider can't translate ordering on a DateTimeOffset column, the same limitation
        // documented on GetDashboardSummaryQuery/GetCustomerDetailQuery.
        var items = (await query.ToListAsync(cancellationToken))
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new CustomerLedgerEntryDto(
                e.Id, e.Type.ToString(), e.Amount, e.BalanceAfter, e.ReferenceType, e.ReferenceId,
                e.Method?.ToString(), e.ReferenceNumber, e.Note, e.CreatedAt))
            .ToList();

        return new PagedResult<CustomerLedgerEntryDto>(items, totalCount, page, pageSize);
    }
}
