namespace ShopKeeper.Application.Suppliers.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Soft delete only (IsActive = false) - existing Products keep their SupplierId
/// and past restock history stays intact, matching every other soft-delete in this codebase.</summary>
public record DeleteSupplierCommand(Guid Id) : IRequest;

public class DeleteSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<DeleteSupplierCommand>
{
    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SuppliersManage);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        supplier.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
