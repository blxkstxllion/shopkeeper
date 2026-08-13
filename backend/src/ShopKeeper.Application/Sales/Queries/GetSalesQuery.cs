namespace ShopKeeper.Application.Sales.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;

public record GetSalesQuery(
    Guid? BranchId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Status,
    int Page,
    int PageSize,
    Guid? CustomerId = null) : IRequest<PagedResult<SaleListItemDto>>;

public class GetSalesQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetSalesQuery, PagedResult<SaleListItemDto>>
{
    public async Task<PagedResult<SaleListItemDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesView);
        if (request.BranchId.HasValue)
        {
            currentUser.RequireBranchAccess(request.BranchId.Value);
        }
        var effectiveBranchId = request.BranchId ?? currentUser.BranchId;

        var query =
            from sale in db.Sales
            join branch in db.Branches on sale.BranchId equals branch.Id
            join cashier in db.Users on sale.CashierUserId equals cashier.Id
            select new { sale, branch.Name, cashier.FirstName, cashier.LastName, ItemCount = sale.Items.Count };

        if (effectiveBranchId.HasValue)
        {
            query = query.Where(x => x.sale.BranchId == effectiveBranchId);
        }

        if (request.From.HasValue)
        {
            query = query.Where(x => x.sale.CreatedAt >= request.From);
        }

        if (request.To.HasValue)
        {
            query = query.Where(x => x.sale.CreatedAt <= request.To);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<Domain.Enums.SaleStatus>(request.Status, out var status))
        {
            query = query.Where(x => x.sale.Status == status);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(x => x.sale.CustomerId == request.CustomerId);
        }

        query = query.OrderByDescending(x => x.sale.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = rows.Select(x => new SaleListItemDto(
            x.sale.Id, x.sale.SaleNumber, x.Name, $"{x.FirstName} {x.LastName}", x.sale.Total, x.sale.GrossProfit,
            x.sale.Status.ToString(), x.ItemCount, x.sale.CreatedAt)).ToList();

        return new PagedResult<SaleListItemDto>(items, totalCount, page, pageSize);
    }
}
