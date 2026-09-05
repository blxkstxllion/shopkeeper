namespace ShopKeeper.Application.Customers.Commands;

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Attributes;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Exceptions;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record UpdateCustomerCommand(
    Guid Id,
    [property: SensitiveData] string Name,
    [property: SensitiveData] string? Phone,
    [property: SensitiveData] string? Email,
    [property: SensitiveData] string? Address,
    bool IsActive,
    Guid? ClientRequestId = null) : IRequest, ISupportsClientRequestId;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateCustomerCommandHandler(IAppDbContext db, ICurrentUserService currentUser) : IRequestHandler<UpdateCustomerCommand>
{
    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone;
        customer.Email = request.Email;
        customer.Address = request.Address;
        customer.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
