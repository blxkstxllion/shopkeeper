namespace ShopKeeper.Application.Businesses.Queries;

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopKeeper.Application.Businesses.Dtos;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>Lists branches for the caller's active business (relies on AppDbContext's tenant query filter).</summary>
public record GetBranchesQuery : IRequest<IReadOnlyList<BranchDto>>;

public class GetBranchesQueryHandler(IAppDbContext db) : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public async Task<IReadOnlyList<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken) =>
        await db.Branches
            .OrderByDescending(b => b.IsMainBranch).ThenBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.Code, b.Address, b.City, b.Country, b.Phone, b.Email, b.IsMainBranch, b.IsActive))
            .ToListAsync(cancellationToken);
}
