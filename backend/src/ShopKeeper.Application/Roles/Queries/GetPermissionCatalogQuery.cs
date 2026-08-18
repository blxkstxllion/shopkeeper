namespace ShopKeeper.Application.Roles.Queries;

using MediatR;
using ShopKeeper.Application.Roles.Dtos;
using ShopKeeper.Domain.Constants;

public record GetPermissionCatalogQuery : IRequest<IReadOnlyList<PermissionCatalogItemDto>>;

/// <summary>Static reference data - PermissionKeys.All is a compile-time list, so this never
/// touches the database. No gate: the catalog itself isn't sensitive, only the ability to
/// grant permissions to a role is (CreateRoleCommand/UpdateRoleCommand).</summary>
public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, IReadOnlyList<PermissionCatalogItemDto>>
{
    public Task<IReadOnlyList<PermissionCatalogItemDto>> Handle(GetPermissionCatalogQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionCatalogItemDto> catalog = PermissionKeys.All
            .Select(p => new PermissionCatalogItemDto(p.Key, p.Name, p.Category))
            .ToList();

        return Task.FromResult(catalog);
    }
}
