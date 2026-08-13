namespace ShopKeeper.Application.Suppliers.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Suppliers.Dtos;

public record GetSuppliersQuery : IRequest<IReadOnlyList<SupplierDto>>;

public class GetSuppliersQueryHandler(IAppDbContext db) : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken) =>
        await db.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive))
            .ToListAsync(cancellationToken);
}
