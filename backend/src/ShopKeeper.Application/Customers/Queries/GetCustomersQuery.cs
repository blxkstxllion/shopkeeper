namespace ShopKeeper.Application.Customers.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Dtos;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Domain.Constants;

public record GetCustomersQuery(string? Search, bool ActiveOnly, int Page, int PageSize) : IRequest<PagedResult<CustomerDto>>;

public class GetCustomersQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    public async Task<PagedResult<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);

        var query = db.Customers.AsQueryable();

        if (request.ActiveOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // .ToLower().Contains() rather than EF.Functions.ILike - see GetProductsQuery for why
            // (Npgsql-only, fails translation under the SQLite provider the test suite runs against).
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term)
                || (c.Phone != null && c.Phone.ToLower().Contains(term))
                || (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.Email, c.Address, c.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items, totalCount, page, pageSize);
    }
}
