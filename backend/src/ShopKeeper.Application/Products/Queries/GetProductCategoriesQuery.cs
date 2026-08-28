namespace ShopKeeper.Application.Products.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Common.Interfaces;
using ShopKeeper.Application.Products.Dtos;

public record GetProductCategoriesQuery : IRequest<IReadOnlyList<ProductCategoryDto>>;

public class GetProductCategoriesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetProductCategoriesQuery, IReadOnlyList<ProductCategoryDto>>
{
    public async Task<IReadOnlyList<ProductCategoryDto>> Handle(GetProductCategoriesQuery request, CancellationToken cancellationToken) =>
        await db.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);
}
