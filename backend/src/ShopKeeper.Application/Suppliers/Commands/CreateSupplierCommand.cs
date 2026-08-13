namespace ShopKeeper.Application.Suppliers.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Suppliers.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateSupplierCommand(string Name, string? ContactName, string? Phone, string? Email, string? Address)
    : IRequest<SupplierDto>;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CreateSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.SuppliersManage);
        var businessId = currentUser.RequireBusinessId();

        var supplier = new Supplier
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            ContactName = request.ContactName,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactName, supplier.Phone, supplier.Email, supplier.Address, supplier.IsActive);
    }
}
