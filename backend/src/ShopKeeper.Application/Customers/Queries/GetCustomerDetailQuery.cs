namespace ShopKeeper.Application.Customers.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;
using ShopKeeper.Domain.Enums;

public record GetCustomerDetailQuery(Guid Id) : IRequest<CustomerDetailDto>;

/// <summary>
/// TotalSpend/AverageSale/LastPurchaseAt are computed in memory after one Sales round trip,
/// not via SUM/MAX in the query - the SQLite provider (test suite only) can't translate
/// DateTimeOffset comparisons/ordering, the same constraint documented on GetDashboardSummaryQuery.
/// Voided sales are excluded, matching every other revenue figure in this app.
/// </summary>
public class GetCustomerDetailQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCustomerDetailQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> Handle(GetCustomerDetailQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        var sales = await db.Sales
            .Where(s => s.CustomerId == request.Id && s.Status != SaleStatus.Voided)
            .Select(s => new { s.Total, s.CreatedAt })
            .ToListAsync(cancellationToken);

        var totalSpend = sales.Sum(s => s.Total);
        var purchaseCount = sales.Count;
        var averageSale = purchaseCount > 0 ? totalSpend / purchaseCount : 0;
        var lastPurchaseAt = sales.Count > 0 ? sales.Max(s => s.CreatedAt) : (DateTimeOffset?)null;

        return new CustomerDetailDto(
            customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.IsActive,
            totalSpend, averageSale, purchaseCount, lastPurchaseAt);
    }
}
