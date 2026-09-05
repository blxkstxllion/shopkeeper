namespace ShopKeeper.Application.Customers.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Attributes;
using ShopKeeper.Application.Common.Behaviors;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Customers.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateCustomerCommand(
    [property: SensitiveData] string Name,
    [property: SensitiveData] string? Phone,
    [property: SensitiveData] string? Email,
    [property: SensitiveData] string? Address,
    Guid? ClientRequestId = null) : IRequest<CustomerDto>, ISupportsClientRequestId;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CreateCustomerCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.CustomersManage);
        var businessId = currentUser.RequireBusinessId();

        var customer = new Customer
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return new CustomerDto(customer.Id, customer.Name, customer.Phone, customer.Email, customer.Address, customer.IsActive);
    }
}
