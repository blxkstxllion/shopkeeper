namespace ShopKeeper.Application.Customers.Commands;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

/// <summary>Soft delete only (IsActive = false) - existing Sales keep their CustomerId and
/// purchase history stays intact, matching every other soft-delete in this codebase.</summary>
public record DeleteCustomerCommand(Guid Id) : IRequest;

public class DeleteCustomerCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        customer.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
