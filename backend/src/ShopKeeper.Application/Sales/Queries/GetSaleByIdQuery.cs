namespace ShopKeeper.Application.Sales.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Sales.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Also serves as the sale's digital receipt view - Items/Payments are always included.</summary>
public record GetSaleByIdQuery(Guid Id) : IRequest<SaleDto>;

public class GetSaleByIdQueryHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<GetSaleByIdQuery, SaleDto>
{
    public async Task<SaleDto> Handle(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SalesView);

        var sale = await db.Sales
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Sale), request.Id);

        var branchName = await db.Branches.Where(b => b.Id == sale.BranchId).Select(b => b.Name).FirstAsync(cancellationToken);
        var cashier = await db.Users.Where(u => u.Id == sale.CashierUserId)
            .Select(u => new { u.FirstName, u.LastName }).FirstAsync(cancellationToken);
        var customerName = sale.CustomerId.HasValue
            ? await db.Customers.Where(c => c.Id == sale.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return SaleMapper.ToDto(sale, branchName, $"{cashier.FirstName} {cashier.LastName}", customerName);
    }
}
