namespace ShopKeeper.Application.Products.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>
/// Soft delete only (IsActive = false) - never hard-deletes, since existing SaleItems,
/// InventoryTransactions, and ProductStocks reference this product and its historical
/// data must remain intact. See section 40: avoid deleting financial records.
/// </summary>
public record DeleteProductCommand(Guid Id, Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class DeleteProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        product.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
