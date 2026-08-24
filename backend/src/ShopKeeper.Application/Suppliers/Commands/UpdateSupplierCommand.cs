namespace ShopKeeper.Application.Suppliers.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Attributes;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateSupplierCommand(
    Guid Id,
    [property: SensitiveData] string Name,
    [property: SensitiveData] string? ContactName,
    [property: SensitiveData] string? Phone,
    [property: SensitiveData] string? Email,
    [property: SensitiveData] string? Address,
    bool IsActive) : IRequest;

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateSupplierCommand>
{
    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SuppliersManage);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.Id);

        supplier.Name = request.Name.Trim();
        supplier.ContactName = request.ContactName;
        supplier.Phone = request.Phone;
        supplier.Email = request.Email;
        supplier.Address = request.Address;
        supplier.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
