namespace ShopKeeper.Application.Products.Commands;

using FluentValidation;
using MediatR;
using ShopKeeper.Application.Common.Extensions;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Products.Dtos;
using ShopKeeper.Domain.Constants;
using ShopKeeper.Domain.Entities;

public record CreateProductCategoryCommand(string Name, string? Description) : IRequest<ProductCategoryDto>;

public class CreateProductCategoryCommandValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateProductCategoryCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateProductCategoryCommand, ProductCategoryDto>
{
    public async Task<ProductCategoryDto> Handle(CreateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        currentUser.RequirePermission(PermissionKeys.ProductsManage);
        var businessId = currentUser.RequireBusinessId();

        var category = new ProductCategory
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Description = request.Description,
        };

        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new ProductCategoryDto(category.Id, category.Name, category.Description, category.IsActive);
    }
}
